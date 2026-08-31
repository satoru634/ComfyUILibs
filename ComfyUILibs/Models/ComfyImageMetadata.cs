namespace ComfyUILibs.Models
{
    /// <summary>
    /// 画像に埋め込まれた ComfyUI / A1111 メタデータの解析結果ステータス。
    /// </summary>
    public enum ComfyMetadataParseStatus
    {
        /// <summary>ポジ／ネガプロンプトなど主要な情報を特定できた。</summary>
        Ok,

        /// <summary>
        /// メタデータは存在するが一部しか特定できなかった
        /// （カスタムサンプラー・独自ノード構成などでリンクを辿り切れなかった場合）。
        /// </summary>
        Partial,

        /// <summary>ComfyUI・A1111 いずれのメタデータも見つからなかった。</summary>
        None,
    }

    /// <summary>ワークフローから抽出した LoRA 1 件分の参照情報。</summary>
    public class ComfyLoraRef
    {
        /// <summary>LoRA ファイル名（<c>LoraLoader</c> ノードの <c>lora_name</c>）。</summary>
        public string Name { get; set; } = "";

        /// <summary>モデル側への適用強度（<c>strength_model</c>）。取得できない場合は null。</summary>
        public double? StrengthModel { get; set; }

        /// <summary>CLIP 側への適用強度（<c>strength_clip</c>）。<c>LoraLoaderModelOnly</c> 等では null。</summary>
        public double? StrengthClip { get; set; }
    }

    /// <summary>
    /// PNG の <c>tEXt</c> チャンク（ComfyUI が埋め込む <c>prompt</c> / <c>workflow</c>、
    /// または A1111 形式の <c>parameters</c>）から抽出した生成情報。
    /// <see cref="Services.IImageMetadataReader"/> が返す。特定できなかった項目は null
    /// （<see cref="Loras"/> は空リスト）になる。
    /// </summary>
    public class ComfyImageMetadata
    {
        /// <summary>ポジティブプロンプト。</summary>
        public string? PositivePrompt { get; set; }

        /// <summary>ネガティブプロンプト。</summary>
        public string? NegativePrompt { get; set; }

        /// <summary>チェックポイント／モデル名（<c>ckpt_name</c> / <c>unet_name</c>）。</summary>
        public string? ModelName { get; set; }

        /// <summary>サンプラー名（<c>sampler_name</c>）。</summary>
        public string? Sampler { get; set; }

        /// <summary>スケジューラー名（<c>scheduler</c>）。</summary>
        public string? Scheduler { get; set; }

        /// <summary>ステップ数。</summary>
        public int? Steps { get; set; }

        /// <summary>CFG スケール。</summary>
        public double? Cfg { get; set; }

        /// <summary>シード値（<c>seed</c> / <c>noise_seed</c>）。</summary>
        public long? Seed { get; set; }

        /// <summary>デノイズ強度。</summary>
        public double? Denoise { get; set; }

        /// <summary>生成画像の幅（<c>EmptyLatentImage</c> 由来、無ければ PNG 実サイズ）。</summary>
        public int? Width { get; set; }

        /// <summary>生成画像の高さ（<c>EmptyLatentImage</c> 由来、無ければ PNG 実サイズ）。</summary>
        public int? Height { get; set; }

        /// <summary>ワークフローに含まれる LoRA の一覧。</summary>
        public IReadOnlyList<ComfyLoraRef> Loras { get; set; } = Array.Empty<ComfyLoraRef>();

        /// <summary>埋め込まれていた ComfyUI <c>prompt</c>（API 形式）JSON の生文字列。無ければ null。</summary>
        public string? RawPromptJson { get; set; }

        /// <summary>埋め込まれていた ComfyUI <c>workflow</c>（UI 形式）JSON の生文字列。無ければ null。</summary>
        public string? RawWorkflowJson { get; set; }

        /// <summary>解析結果のステータス。</summary>
        public ComfyMetadataParseStatus ParseStatus { get; set; } = ComfyMetadataParseStatus.None;
    }
}
