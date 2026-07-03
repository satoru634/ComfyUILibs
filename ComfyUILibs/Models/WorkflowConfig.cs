using System.Text.Json.Serialization;

namespace ComfyUILibs.Models
{
    /// <summary>画像の幅と高さをピクセル単位で保持する。workflow_config.json および入力 JSON の image_size に対応。</summary>
    public class ImageSize
    {
        /// <summary>画像の幅（ピクセル）。512〜2048 の範囲かつ 8 の倍数であること。</summary>
        [JsonPropertyName("width")]
        public int Width { get; set; }

        /// <summary>画像の高さ（ピクセル）。512〜2048 の範囲かつ 8 の倍数であること。</summary>
        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    /// <summary>
    /// workflow_config.json の workflows[name].loras に定義された LoRA エントリ。
    /// キーがユーザー向けの論理名、このクラスが実ファイル名と強度を保持する。
    /// </summary>
    public class LoraEntry
    {
        /// <summary>LoRA の実ファイル名（例: my_lora.safetensors）。空でない文字列であること。</summary>
        [JsonPropertyName("file")]
        public string? File { get; set; }

        /// <summary>LoRA の適用強度。null の場合は設定ファイルに strength キーが存在しないことを示す。</summary>
        [JsonPropertyName("strength")]
        public double? Strength { get; set; }
    }

    /// <summary>
    /// workflow_config.json の workflows[name] に対応するワークフロー別設定。
    /// テンプレート選択・デフォルト画像サイズ・LoRA マッピングを保持する。
    /// </summary>
    public class WorkflowSettings
    {
        /// <summary>入力 JSON で image_size を省略した場合に使用するデフォルト画像サイズ。</summary>
        [JsonPropertyName("default_image_size")]
        public ImageSize? DefaultImageSize { get; set; }

        /// <summary>
        /// 画像の向きごとのサイズマップ。"vertical"・"horizontal"・"square" の 3 キーが必須。
        /// generate_image_bot などの外部ツールからも参照される。
        /// </summary>
        [JsonPropertyName("image_size")]
        public Dictionary<string, ImageSize>? ImageSize { get; set; }

        /// <summary>
        /// LoRA の論理名（キー）と実ファイル名・強度（値）のマッピング。
        /// 入力 JSON の loras リストで指定する名前はこのキーと一致していること。
        /// </summary>
        [JsonPropertyName("loras")]
        public Dictionary<string, LoraEntry>? Loras { get; set; }
    }

    /// <summary>
    /// workflow_config.json の wd14_tagger セクションに対応する WD Timm Tagger の設定。
    /// Wd14TaggerRunner から参照される。
    /// </summary>
    public class Wd14TaggerConfig
    {
        /// <summary>使用する WD Timm Tagger のモデル名（例: wd-eva02-large-tagger-v3）。</summary>
        [JsonPropertyName("model_name")]
        public string? ModelName { get; set; }

        /// <summary>一般タグを出力するしきい値（0.0〜1.0）。null の場合はキーが存在しないことを示す。</summary>
        [JsonPropertyName("general_threshold")]
        public double? GeneralThreshold { get; set; }

        /// <summary>キャラクタータグを出力するしきい値（0.0〜1.0）。null の場合はキーが存在しないことを示す。</summary>
        [JsonPropertyName("character_threshold")]
        public double? CharacterThreshold { get; set; }
    }

    /// <summary>
    /// workflow_config.json 全体に対応するルートオブジェクト。
    /// <see cref="ConfigLoader.LoadConfig"/> でファイルから読み込まれバリデーションされる。
    /// </summary>
    public class WorkflowConfig
    {
        /// <summary>ComfyUI サーバーの接続先 URL（例: http://127.0.0.1:8188）。</summary>
        [JsonPropertyName("comfyui_url")]
        public string? ComfyuiUrl { get; set; }

        /// <summary>--workflow オプション省略時に使用するワークフロー名。workflows のキーと一致していること。</summary>
        [JsonPropertyName("default_workflow")]
        public string? DefaultWorkflow { get; set; }

        /// <summary>ワークフロー名をキーとした設定の辞書。各値は <see cref="WorkflowSettings"/> で定義される。</summary>
        [JsonPropertyName("workflows")]
        public Dictionary<string, WorkflowSettings>? Workflows { get; set; }

        /// <summary>WD Timm Tagger の設定。このセクションが存在する場合のみ <see cref="Wd14TaggerRunner"/> が利用できる。</summary>
        [JsonPropertyName("wd14_tagger")]
        public Wd14TaggerConfig? Wd14Tagger { get; set; }
    }
}
