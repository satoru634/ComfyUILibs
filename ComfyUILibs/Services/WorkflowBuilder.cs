using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// ワークフローテンプレートの選択・読み込み・プロンプト/LoRA/サイズ/シード値の適用を担うクラス。
    /// Python 版の workflow_builder.py を移植したもの。
    /// </summary>
    public class WorkflowBuilder
    {
        /// <summary>テンプレートファイルが格納されているルートディレクトリのパス。</summary>
        private readonly string _templatesDir;

        /// <summary>
        /// テンプレートディレクトリのパスを指定してインスタンスを初期化する。
        /// </summary>
        /// <param name="templatesDir">templates/ ディレクトリの絶対パスまたはベースディレクトリからの相対パス。</param>
        public WorkflowBuilder(string templatesDir)
        {
            _templatesDir = templatesDir;
        }

        // ── テンプレート選択 ───────────────────────────────────────────────

        /// <summary>
        /// LoRA 数とワークフロー名から使用すべきテンプレートファイルのパスを決定する。
        /// テンプレートは <c>templates/{workflowName}/template_lora_{N}.json</c> に配置されていること。
        /// </summary>
        /// <param name="loraCount">使用する LoRA の数（0〜4）。</param>
        /// <param name="workflowName">ワークフロー名（workflow_config.json の workflows キーに一致）。</param>
        /// <returns>テンプレートファイルの絶対パス。</returns>
        /// <exception cref="ComfyUIException">LoRA 数が範囲外・ディレクトリ欠落・ファイル欠落の場合。</exception>
        public string SelectTemplate(int loraCount, string workflowName)
        {
            if (loraCount < 0 || loraCount > 4)
                throw new ComfyUIException(
                    $"LoRA は 0〜4 個の範囲で指定してください（指定数: {loraCount}）");

            var workflowDir = Path.Combine(_templatesDir, workflowName);
            if (!Directory.Exists(workflowDir))
                throw new ComfyUIException(
                    $"テンプレートディレクトリが見つかりません: {workflowName}");

            var path = Path.Combine(workflowDir, $"template_lora_{loraCount}.json");
            if (!File.Exists(path))
                throw new ComfyUIException(
                    $"テンプレートファイルが見つかりません: {workflowName}/template_lora_{loraCount}.json");

            return path;
        }

        // ── テンプレート読み込み ───────────────────────────────────────────

        /// <summary>
        /// テンプレート JSON ファイルを読み込み、ミュータブルな <see cref="JsonObject"/> として返す。
        /// </summary>
        /// <param name="templatePath">テンプレートファイルのパス（<see cref="SelectTemplate"/> の戻り値）。</param>
        /// <returns>テンプレートの JSON オブジェクト。</returns>
        /// <exception cref="ComfyUIException">ファイル欠落・JSON 不正の場合。</exception>
        public JsonObject LoadTemplate(string templatePath)
        {
            string json;
            try
            {
                json = File.ReadAllText(templatePath, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                throw new ComfyUIException(
                    $"テンプレートファイルが見つかりません: {Path.GetFileName(templatePath)}");
            }

            try
            {
                return JsonNode.Parse(json)?.AsObject()
                    ?? throw new ComfyUIException(
                        $"テンプレート JSON の解析に失敗しました ({Path.GetFileName(templatePath)})");
            }
            catch (JsonException ex)
            {
                throw new ComfyUIException(
                    $"テンプレート JSON の解析に失敗しました ({Path.GetFileName(templatePath)}): {ex.Message}", ex);
            }
        }

        // ── テンプレート適用 ───────────────────────────────────────────────

        /// <summary>
        /// 渡されたワークフローに対してプロンプト・LoRA・画像サイズ・シード値を適用した新しい
        /// <see cref="JsonObject"/> を返す。元のワークフローは変更しない（DeepCopy してから処理）。
        /// </summary>
        /// <param name="workflow">ベースとなるワークフロー JSON（<see cref="LoadTemplate"/> の戻り値）。</param>
        /// <param name="prompts">正・負プロンプトのペア。</param>
        /// <param name="resolvedLoras">解決済み LoRA エントリのリスト。</param>
        /// <param name="seed">シード値。null の場合は 0〜2^53 の乱数を自動生成する。</param>
        /// <param name="imageSize">画像サイズ。null の場合はテンプレートのデフォルト値を維持する。</param>
        /// <returns>各値が適用された新しいワークフロー JSON。</returns>
        /// <exception cref="ComfyUIException">必要なノードがテンプレートに存在しない場合。</exception>
        public JsonObject Apply(
            JsonObject workflow,
            PromptPair prompts,
            List<ResolvedLora> resolvedLoras,
            long? seed = null,
            ImageSize? imageSize = null)
        {
            // テンプレートを直接書き換えないよう DeepCopy してから処理する
            var cloned = JsonNode.Parse(workflow.ToJsonString())!.AsObject();

            // _meta.title → ノード の逆引きマップを構築してノードを名前で検索できるようにする
            var titleMap = BuildTitleMap(cloned);

            ApplyPrompts(titleMap, prompts);
            ApplyLoras(titleMap, resolvedLoras);
            if (imageSize != null)
                ApplyImageSize(titleMap, imageSize);
            // seed が null の場合は Python 版と同様に 0〜2^53 の乱数を使用する
            ApplySeeds(cloned, seed ?? Random.Shared.NextInt64(0, (1L << 53) + 1));

            return cloned;
        }

        /// <summary>
        /// ワークフロー内の全ノードを走査し、<c>_meta.title</c> をキーとする逆引きマップを構築する。
        /// </summary>
        private static Dictionary<string, JsonObject> BuildTitleMap(JsonObject workflow)
        {
            var map = new Dictionary<string, JsonObject>();
            foreach (var kvp in workflow)
            {
                if (kvp.Value is JsonObject node
                    && node["_meta"] is JsonObject meta
                    && meta["title"] is JsonValue titleVal)
                {
                    map[titleVal.GetValue<string>()] = node;
                }
            }
            return map;
        }

        /// <summary>
        /// <c>positive_prompt</c> ノードと <c>negative_prompt</c> ノードの <c>inputs.text</c> を設定する。
        /// </summary>
        private static void ApplyPrompts(Dictionary<string, JsonObject> titleMap, PromptPair prompts)
        {
            foreach (var (title, field) in new[]
            {
                ("positive_prompt", prompts.Positive),
                ("negative_prompt", prompts.Negative)
            })
            {
                if (!titleMap.TryGetValue(title, out var node))
                    throw new ComfyUIException($"テンプレートにノード '{title}' が見つかりません");
                node["inputs"]!.AsObject()["text"] = JsonValue.Create(field);
            }
        }

        /// <summary>
        /// <c>empty_latent_image</c> ノードの <c>inputs.width</c> と <c>inputs.height</c> を設定する。
        /// </summary>
        private static void ApplyImageSize(Dictionary<string, JsonObject> titleMap, ImageSize imageSize)
        {
            const string key = "empty_latent_image";
            if (!titleMap.TryGetValue(key, out var node))
                throw new ComfyUIException($"テンプレートにノード '{key}' が見つかりません");
            var inputs = node["inputs"]!.AsObject();
            inputs["width"] = JsonValue.Create(imageSize.Width);
            inputs["height"] = JsonValue.Create(imageSize.Height);
        }

        /// <summary>
        /// 解決済み LoRA リストを <c>lora_loader_1</c>〜<c>lora_loader_N</c> ノードに設定する。
        /// </summary>
        private static void ApplyLoras(Dictionary<string, JsonObject> titleMap, List<ResolvedLora> resolvedLoras)
        {
            for (int i = 0; i < resolvedLoras.Count; i++)
            {
                var lora = resolvedLoras[i];
                var key = $"lora_loader_{i + 1}";
                if (!titleMap.TryGetValue(key, out var node))
                    throw new ComfyUIException($"テンプレートにノード '{key}' が見つかりません");
                var inputs = node["inputs"]!.AsObject();
                inputs["lora_name"] = JsonValue.Create(lora.File);
                inputs["strength_model"] = JsonValue.Create(lora.Strength);
            }
        }

        /// <summary>
        /// <c>inputs.seed</c> フィールドを持つ全ノードに同一のシード値を設定する。
        /// 複数のサンプラーノードが存在する場合でも同一シードで再現性を確保するため全ノードに適用する。
        /// </summary>
        private static void ApplySeeds(JsonObject workflow, long seed)
        {
            foreach (var kvp in workflow)
            {
                if (kvp.Value is JsonObject node
                    && node["inputs"] is JsonObject inputs
                    && inputs.ContainsKey("seed"))
                {
                    inputs["seed"] = JsonValue.Create(seed);
                }
            }
        }
    }
}
