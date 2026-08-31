using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Resources;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// <see cref="IImageMetadataReader"/> の既定実装。
    /// PNG のチャンク構造を直接走査して <c>tEXt</c> / <c>iTXt</c> / <c>zTXt</c> を取り出し、
    /// ComfyUI が埋め込む <c>prompt</c>（API 形式ノードグラフ）を優先的に解析する。
    /// <c>prompt</c> が無い場合は A1111 形式の <c>parameters</c> テキストをフォールバック解析する。
    /// </summary>
    /// <remarks>
    /// <c>prompt</c> の解析は「<c>KSampler</c> 系ノードの <c>positive</c> / <c>negative</c> 入力を辿って
    /// <c>CLIPTextEncode</c> 系ノードの <c>text</c> を取得する」ことをベストエフォートで行う。
    /// カスタムサンプラーや独自ノード構成でリンクを辿り切れない場合は、全 <c>CLIPTextEncode</c> の
    /// <c>text</c> を連結してポジ扱いとし <see cref="ComfyMetadataParseStatus.Partial"/> を返す。
    /// </remarks>
    public class ImageMetadataReader : IImageMetadataReader
    {
        /// <summary>PNG シグネチャ（先頭 8 バイト）。</summary>
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>テキストを保持する PNG チャンクタイプ。</summary>
        private static readonly HashSet<string> TextChunkTypes = new(StringComparer.Ordinal) { "tEXt", "iTXt", "zTXt" };

        /// <summary>1 チャンクの最大許容データ長（壊れたファイルで巨大 alloc を避けるためのガード）。</summary>
        private const int MaxChunkLength = 64 * 1024 * 1024;

        /// <summary>リンク（<c>["nodeId", slot]</c>）を辿る最大深さ。</summary>
        private const int MaxLinkDepth = 6;

        /// <inheritdoc/>
        public ComfyImageMetadata Read(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new ComfyUIException(Messages.Get("ImageMetadataReader_FileNotFound_Format", imagePath));

            PngChunks chunks;
            try
            {
                chunks = ReadPngChunks(imagePath);
            }
            catch (Exception ex) when (ex is not ComfyUIException)
            {
                throw new ComfyUIException(Messages.Get("ImageMetadataReader_ReadFailed_Format", imagePath), ex);
            }

            var metadata = new ComfyImageMetadata
            {
                RawPromptJson = chunks.Texts.GetValueOrDefault("prompt"),
                RawWorkflowJson = chunks.Texts.GetValueOrDefault("workflow"),
            };
            if (chunks.Width > 0) metadata.Width = chunks.Width;
            if (chunks.Height > 0) metadata.Height = chunks.Height;

            var parameters = chunks.Texts.GetValueOrDefault("parameters")
                             ?? chunks.Texts.GetValueOrDefault("Parameters");

            if (!string.IsNullOrWhiteSpace(metadata.RawPromptJson) && TryParseComfyPrompt(metadata.RawPromptJson!, metadata))
            {
                // 解析ステータスは TryParseComfyPrompt 内で設定済み
            }
            else if (!string.IsNullOrWhiteSpace(parameters))
            {
                ParseAutomatic1111Parameters(parameters!, metadata);
            }
            else if (!string.IsNullOrWhiteSpace(metadata.RawWorkflowJson))
            {
                // UI 形式の workflow のみ埋め込まれているケース。生 JSON は保持するが構造化解析は行わない。
                metadata.ParseStatus = ComfyMetadataParseStatus.Partial;
            }
            else
            {
                metadata.ParseStatus = ComfyMetadataParseStatus.None;
            }

            return metadata;
        }

        // ----- PNG チャンク走査 -----

        private readonly struct PngChunks
        {
            public PngChunks(Dictionary<string, string> texts, int width, int height)
            {
                Texts = texts;
                Width = width;
                Height = height;
            }

            /// <summary>チャンクキーワード → テキスト値（先勝ち）。</summary>
            public Dictionary<string, string> Texts { get; }

            /// <summary>IHDR 由来の画像幅（取得できなければ 0）。</summary>
            public int Width { get; }

            /// <summary>IHDR 由来の画像高さ（取得できなければ 0）。</summary>
            public int Height { get; }
        }

        private static PngChunks ReadPngChunks(string path)
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            int width = 0, height = 0;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            var signature = new byte[8];
            if (!TryReadExact(stream, signature, 8) || !signature.AsSpan().SequenceEqual(PngSignature))
                return new PngChunks(texts, 0, 0); // PNG でない／破損 → メタデータ無し扱い

            var header = new byte[8]; // 長さ(4) + タイプ(4)
            while (TryReadExact(stream, header, 8))
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                var type = Encoding.ASCII.GetString(header, 4, 4);

                if (type == "IEND")
                    break;

                if (length > MaxChunkLength)
                    break; // 壊れているとみなして打ち切り

                if (type == "IHDR")
                {
                    var ihdr = new byte[length];
                    if (!TryReadExact(stream, ihdr, (int)length))
                        break;
                    if (length >= 8)
                    {
                        width = (int)BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(0, 4));
                        height = (int)BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(4, 4));
                    }
                    stream.Seek(4, SeekOrigin.Current); // CRC
                }
                else if (TextChunkTypes.Contains(type))
                {
                    var data = new byte[length];
                    if (!TryReadExact(stream, data, (int)length))
                        break;
                    stream.Seek(4, SeekOrigin.Current); // CRC

                    if (TryParseTextChunk(type, data, out var keyword, out var value)
                        && !texts.ContainsKey(keyword))
                    {
                        texts[keyword] = value;
                    }
                }
                else
                {
                    // その他のチャンク（IDAT 等）はデータ + CRC をスキップ
                    stream.Seek(length + 4, SeekOrigin.Current);
                }
            }

            return new PngChunks(texts, width, height);
        }

        private static bool TryReadExact(Stream stream, byte[] buffer, int count)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }
            return true;
        }

        private static bool TryParseTextChunk(string type, byte[] data, out string keyword, out string value)
        {
            keyword = "";
            value = "";

            var nul = Array.IndexOf(data, (byte)0);
            if (nul < 0)
                return false;

            // キーワードは Latin-1（ISO-8859-1）
            keyword = Encoding.Latin1.GetString(data, 0, nul);

            switch (type)
            {
                case "tEXt":
                {
                    // 残りは Latin-1 の非圧縮テキスト。ComfyUI の JSON は ensure_ascii のため ASCII 範囲。
                    value = Encoding.Latin1.GetString(data, nul + 1, data.Length - nul - 1);
                    return true;
                }
                case "zTXt":
                {
                    // nul の次の 1 バイトが圧縮方式（0 = zlib/deflate）
                    if (nul + 2 > data.Length)
                        return false;
                    var compressed = data.AsSpan(nul + 2).ToArray();
                    if (!TryInflate(compressed, out var bytes))
                        return false;
                    value = Encoding.Latin1.GetString(bytes);
                    return true;
                }
                case "iTXt":
                {
                    // keyword \0 compressionFlag(1) compressionMethod(1) langTag \0 translatedKeyword \0 text(UTF-8)
                    var pos = nul + 1;
                    if (pos + 2 > data.Length)
                        return false;
                    var compressionFlag = data[pos];
                    pos += 2; // compressionFlag + compressionMethod

                    var langEnd = Array.IndexOf(data, (byte)0, pos);
                    if (langEnd < 0)
                        return false;
                    var transEnd = Array.IndexOf(data, (byte)0, langEnd + 1);
                    if (transEnd < 0)
                        return false;

                    var textBytes = data.AsSpan(transEnd + 1).ToArray();
                    if (compressionFlag == 1)
                    {
                        if (!TryInflate(textBytes, out textBytes))
                            return false;
                    }
                    value = Encoding.UTF8.GetString(textBytes);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool TryInflate(byte[] zlibData, out byte[] result)
        {
            try
            {
                using var input = new MemoryStream(zlibData);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                result = output.ToArray();
                return true;
            }
            catch
            {
                result = Array.Empty<byte>();
                return false;
            }
        }

        // ----- ComfyUI prompt（API 形式）解析 -----

        private readonly record struct PromptNode(string ClassType, JsonElement Inputs);

        /// <summary>
        /// ComfyUI の <c>prompt</c> JSON を解析して <paramref name="metadata"/> を埋める。
        /// JSON として妥当なオブジェクトであれば（抽出できた情報の多寡に関わらず）true を返し、
        /// <see cref="ComfyImageMetadata.ParseStatus"/> を設定する。JSON 自体が壊れている場合は false。
        /// </summary>
        private static bool TryParseComfyPrompt(string json, ComfyImageMetadata metadata)
        {
            Dictionary<string, PromptNode> nodes;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                nodes = new Dictionary<string, PromptNode>(StringComparer.Ordinal);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    if (!property.Value.TryGetProperty("class_type", out var classType)
                        || classType.ValueKind != JsonValueKind.String)
                        continue;
                    property.Value.TryGetProperty("inputs", out var inputs);
                    nodes[property.Name] = new PromptNode(classType.GetString() ?? "", inputs.Clone());
                }
            }
            catch (JsonException)
            {
                return false;
            }

            var samplerNode = FindSamplerNode(nodes);

            string? positive = null;
            string? negative = null;
            if (samplerNode is { } sampler)
            {
                var conditioningSource = sampler.Inputs;
                // SamplerCustomAdvanced 等: positive/negative を持たず guider 経由の場合
                if (!HasInput(sampler.Inputs, "positive") && TryGetElement(sampler.Inputs, "guider", out var guiderLink))
                {
                    if (TryResolveNode(nodes, guiderLink, out var guiderNode))
                        conditioningSource = guiderNode.Inputs;
                }

                if (TryGetElement(conditioningSource, "positive", out var positiveLink))
                    positive = ResolvePromptText(nodes, positiveLink, 0);
                if (TryGetElement(conditioningSource, "negative", out var negativeLink))
                    negative = ResolvePromptText(nodes, negativeLink, 0);

                ExtractSamplerParameters(sampler.Inputs, metadata);
            }

            // サンプラー経由で取れなかった場合のフォールバック: 全 CLIPTextEncode の text を連結
            var usedFallback = false;
            if (positive is null && negative is null)
            {
                var encoded = CollectClipTextEncodeTexts(nodes);
                if (encoded.Count > 0)
                {
                    positive = string.Join("\n", encoded);
                    usedFallback = true;
                }
            }

            metadata.PositivePrompt = NormalizeWhitespaceOrNull(positive);
            metadata.NegativePrompt = NormalizeWhitespaceOrNull(negative);
            metadata.ModelName ??= FindModelName(nodes);
            ApplyLatentSize(nodes, metadata);
            metadata.Loras = CollectLoras(nodes);

            var resolvedBoth = metadata.PositivePrompt is not null && metadata.NegativePrompt is not null;
            metadata.ParseStatus = (samplerNode is not null && resolvedBoth && !usedFallback)
                ? ComfyMetadataParseStatus.Ok
                : ComfyMetadataParseStatus.Partial;
            return true;
        }

        private static PromptNode? FindSamplerNode(Dictionary<string, PromptNode> nodes)
        {
            PromptNode? firstSamplerLike = null;
            foreach (var node in nodes.Values)
            {
                if (!IsSamplerClass(node.ClassType))
                    continue;

                firstSamplerLike ??= node;
                if (HasInput(node.Inputs, "positive") || HasInput(node.Inputs, "guider"))
                    return node; // 条件付け入力を持つサンプラーを優先
            }
            return firstSamplerLike;
        }

        private static bool IsSamplerClass(string classType)
            => classType.Contains("KSampler", StringComparison.OrdinalIgnoreCase)
               || classType.StartsWith("SamplerCustom", StringComparison.OrdinalIgnoreCase);

        private static string? ResolvePromptText(Dictionary<string, PromptNode> nodes, JsonElement link, int depth)
        {
            if (depth > MaxLinkDepth)
                return null;

            if (link.ValueKind == JsonValueKind.String)
                return link.GetString();

            if (!TryResolveNode(nodes, link, out var node))
                return null;

            var inputs = node.Inputs;

            if (node.ClassType.Contains("CLIPTextEncode", StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetElement(inputs, "text", out var textElement))
                {
                    if (textElement.ValueKind == JsonValueKind.String)
                        return textElement.GetString();
                    if (textElement.ValueKind == JsonValueKind.Array)
                        return ResolvePromptText(nodes, textElement, depth + 1);
                }

                // SDXL 版（text_g / text_l）
                var g = TryGetString(inputs, "text_g");
                var l = TryGetString(inputs, "text_l");
                if (g is not null || l is not null)
                    return string.IsNullOrEmpty(l) || l == g ? g : $"{g}, {l}";
            }

            // Note / PrimitiveNode / 文字列リテラルノード等
            foreach (var key in new[] { "text", "string", "String", "value", "prompt" })
            {
                var literal = TryGetString(inputs, key);
                if (literal is not null)
                    return literal;
            }

            // ConditioningCombine / ConditioningConcat 等: 上流のリンクを順に辿る
            if (inputs.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in inputs.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                        continue;
                    var resolved = ResolvePromptText(nodes, property.Value, depth + 1);
                    if (!string.IsNullOrEmpty(resolved))
                        return resolved;
                }
            }

            return null;
        }

        private static List<string> CollectClipTextEncodeTexts(Dictionary<string, PromptNode> nodes)
        {
            var texts = new List<string>();
            foreach (var node in nodes.Values)
            {
                if (!node.ClassType.Contains("CLIPTextEncode", StringComparison.OrdinalIgnoreCase))
                    continue;
                var text = TryGetString(node.Inputs, "text") ?? TryGetString(node.Inputs, "text_g");
                if (!string.IsNullOrWhiteSpace(text))
                    texts.Add(text!);
            }
            return texts;
        }

        private static void ExtractSamplerParameters(JsonElement inputs, ComfyImageMetadata metadata)
        {
            metadata.Sampler ??= TryGetString(inputs, "sampler_name");
            metadata.Scheduler ??= TryGetString(inputs, "scheduler");
            if (metadata.Steps is null && TryGetInt(inputs, "steps", out var steps)) metadata.Steps = steps;
            if (metadata.Cfg is null && TryGetDouble(inputs, "cfg", out var cfg)) metadata.Cfg = cfg;
            if (metadata.Denoise is null && TryGetDouble(inputs, "denoise", out var denoise)) metadata.Denoise = denoise;
            if (metadata.Seed is null)
            {
                if (TryGetLong(inputs, "seed", out var seed)) metadata.Seed = seed;
                else if (TryGetLong(inputs, "noise_seed", out var noiseSeed)) metadata.Seed = noiseSeed;
            }
        }

        private static string? FindModelName(Dictionary<string, PromptNode> nodes)
        {
            foreach (var node in nodes.Values)
            {
                if (node.ClassType.Contains("CheckpointLoader", StringComparison.OrdinalIgnoreCase))
                {
                    var ckpt = TryGetString(node.Inputs, "ckpt_name");
                    if (ckpt is not null) return ckpt;
                }
                if (node.ClassType.Contains("UNETLoader", StringComparison.OrdinalIgnoreCase)
                    || node.ClassType.Contains("UnetLoader", StringComparison.OrdinalIgnoreCase))
                {
                    var unet = TryGetString(node.Inputs, "unet_name");
                    if (unet is not null) return unet;
                }
            }
            return null;
        }

        private static void ApplyLatentSize(Dictionary<string, PromptNode> nodes, ComfyImageMetadata metadata)
        {
            foreach (var node in nodes.Values)
            {
                if (!node.ClassType.Contains("EmptyLatentImage", StringComparison.OrdinalIgnoreCase)
                    && !node.ClassType.Contains("EmptySD3LatentImage", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (TryGetInt(node.Inputs, "width", out var width) && width > 0) metadata.Width = width;
                if (TryGetInt(node.Inputs, "height", out var height) && height > 0) metadata.Height = height;
                return;
            }
        }

        private static IReadOnlyList<ComfyLoraRef> CollectLoras(Dictionary<string, PromptNode> nodes)
        {
            var loras = new List<ComfyLoraRef>();
            foreach (var node in nodes.Values)
            {
                if (!node.ClassType.Contains("LoraLoader", StringComparison.OrdinalIgnoreCase))
                    continue;
                var name = TryGetString(node.Inputs, "lora_name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var lora = new ComfyLoraRef { Name = name! };
                if (TryGetDouble(node.Inputs, "strength_model", out var sm)) lora.StrengthModel = sm;
                if (TryGetDouble(node.Inputs, "strength_clip", out var sc)) lora.StrengthClip = sc;
                loras.Add(lora);
            }
            return loras.Count > 0 ? loras : Array.Empty<ComfyLoraRef>();
        }

        // ----- A1111 parameters フォールバック -----

        private static readonly Regex A1111StepsLine = new(@"(^|,)\s*Steps:\s*\d", RegexOptions.Compiled);
        private static readonly Regex A1111Steps = new(@"Steps:\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex A1111Sampler = new(@"Sampler:\s*([^,]+)", RegexOptions.Compiled);
        private static readonly Regex A1111Scheduler = new(@"Schedule type:\s*([^,]+)", RegexOptions.Compiled);
        private static readonly Regex A1111Cfg = new(@"CFG scale:\s*([\d.]+)", RegexOptions.Compiled);
        private static readonly Regex A1111Seed = new(@"Seed:\s*(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex A1111Size = new(@"Size:\s*(\d+)x(\d+)", RegexOptions.Compiled);
        private static readonly Regex A1111Model = new(@"Model:\s*([^,]+)", RegexOptions.Compiled);
        private static readonly Regex A1111Denoise = new(@"Denoising strength:\s*([\d.]+)", RegexOptions.Compiled);

        private static void ParseAutomatic1111Parameters(string text, ComfyImageMetadata metadata)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            var negativeIndex = -1;
            var paramIndex = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (negativeIndex < 0 && lines[i].TrimStart().StartsWith("Negative prompt:", StringComparison.Ordinal))
                    negativeIndex = i;
                if (A1111StepsLine.IsMatch(lines[i]))
                    paramIndex = i;
            }

            var positiveEnd = negativeIndex >= 0 ? negativeIndex : (paramIndex >= 0 ? paramIndex : lines.Length);
            var positive = string.Join("\n", lines[..positiveEnd]).Trim();
            metadata.PositivePrompt = NormalizeWhitespaceOrNull(positive);

            if (negativeIndex >= 0)
            {
                var negativeEnd = paramIndex > negativeIndex ? paramIndex : lines.Length;
                var firstLine = lines[negativeIndex].TrimStart()["Negative prompt:".Length..];
                var negativeLines = new List<string> { firstLine };
                negativeLines.AddRange(lines[(negativeIndex + 1)..negativeEnd]);
                metadata.NegativePrompt = NormalizeWhitespaceOrNull(string.Join("\n", negativeLines).Trim());
            }

            var paramLine = paramIndex >= 0 ? string.Join(" ", lines[paramIndex..]) : "";
            if (paramLine.Length > 0)
            {
                if (A1111Steps.Match(paramLine) is { Success: true } s && int.TryParse(s.Groups[1].Value, out var steps))
                    metadata.Steps = steps;
                if (A1111Sampler.Match(paramLine) is { Success: true } sampler)
                    metadata.Sampler = sampler.Groups[1].Value.Trim();
                if (A1111Scheduler.Match(paramLine) is { Success: true } scheduler)
                    metadata.Scheduler = scheduler.Groups[1].Value.Trim();
                if (A1111Cfg.Match(paramLine) is { Success: true } cfg
                    && double.TryParse(cfg.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cfgValue))
                    metadata.Cfg = cfgValue;
                if (A1111Seed.Match(paramLine) is { Success: true } seed && long.TryParse(seed.Groups[1].Value, out var seedValue))
                    metadata.Seed = seedValue;
                if (A1111Size.Match(paramLine) is { Success: true } size
                    && int.TryParse(size.Groups[1].Value, out var w) && int.TryParse(size.Groups[2].Value, out var h))
                {
                    metadata.Width = w;
                    metadata.Height = h;
                }
                if (A1111Model.Match(paramLine) is { Success: true } model)
                    metadata.ModelName = model.Groups[1].Value.Trim();
                if (A1111Denoise.Match(paramLine) is { Success: true } denoise
                    && double.TryParse(denoise.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var denoiseValue))
                    metadata.Denoise = denoiseValue;
            }

            metadata.ParseStatus = metadata.PositivePrompt is not null && metadata.NegativePrompt is not null
                ? ComfyMetadataParseStatus.Ok
                : ComfyMetadataParseStatus.Partial;
        }

        // ----- JSON 入力読み取りヘルパー -----

        private static bool HasInput(JsonElement inputs, string name)
            => inputs.ValueKind == JsonValueKind.Object && inputs.TryGetProperty(name, out _);

        private static bool TryGetElement(JsonElement inputs, string name, out JsonElement element)
        {
            if (inputs.ValueKind == JsonValueKind.Object && inputs.TryGetProperty(name, out element))
                return true;
            element = default;
            return false;
        }

        private static bool TryResolveNode(Dictionary<string, PromptNode> nodes, JsonElement link, out PromptNode node)
        {
            node = default;
            if (link.ValueKind != JsonValueKind.Array || link.GetArrayLength() < 1)
                return false;

            var idElement = link[0];
            var id = idElement.ValueKind == JsonValueKind.Number ? idElement.GetRawText() : idElement.GetString();
            return id is not null && nodes.TryGetValue(id, out node);
        }

        private static string? TryGetString(JsonElement inputs, string name)
        {
            if (TryGetElement(inputs, name, out var element) && element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                return string.IsNullOrEmpty(value) ? null : value;
            }
            return null;
        }

        private static bool TryGetInt(JsonElement inputs, string name, out int value)
        {
            value = 0;
            if (!TryGetElement(inputs, name, out var element))
                return false;
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out value)) return true;
                if (element.TryGetDouble(out var d)) { value = (int)d; return true; }
                return false;
            }
            return element.ValueKind == JsonValueKind.String
                   && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetLong(JsonElement inputs, string name, out long value)
        {
            value = 0;
            if (!TryGetElement(inputs, name, out var element))
                return false;
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt64(out value)) return true;
                if (element.TryGetDouble(out var d)) { value = (long)d; return true; }
                return false;
            }
            return element.ValueKind == JsonValueKind.String
                   && long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetDouble(JsonElement inputs, string name, out double value)
        {
            value = 0;
            if (!TryGetElement(inputs, name, out var element))
                return false;
            if (element.ValueKind == JsonValueKind.Number)
                return element.TryGetDouble(out value);
            return element.ValueKind == JsonValueKind.String
                   && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string? NormalizeWhitespaceOrNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
