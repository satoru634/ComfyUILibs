using System.Text.Json.Serialization;

namespace ComfyUILibs.Models
{
    /// <summary>入力 JSON の prompts フィールドに対応する正・負プロンプトのペア。</summary>
    public class PromptPair
    {
        /// <summary>ポジティブプロンプト（生成したい内容）。最大 3000 文字。</summary>
        [JsonPropertyName("positive")]
        public string Positive { get; set; } = "";

        /// <summary>ネガティブプロンプト（生成から除外したい内容）。最大 3000 文字。</summary>
        [JsonPropertyName("negative")]
        public string Negative { get; set; } = "";
    }

    /// <summary>
    /// ワークフロー実行時の入力 JSON 全体に対応するオブジェクト。
    /// <see cref="ConfigLoader.LoadAndValidateInput"/> でファイルから読み込まれバリデーションされる。
    /// </summary>
    public class WorkflowInput
    {
        /// <summary>使用する LoRA の論理名リスト（最大 4 件）。config.json の loras キーと一致していること。</summary>
        [JsonPropertyName("loras")]
        public List<string> Loras { get; set; } = new();

        /// <summary>正・負プロンプトのペア。</summary>
        [JsonPropertyName("prompts")]
        public PromptPair Prompts { get; set; } = new();

        /// <summary>生成する画像のサイズ。省略時は対象ワークフローの default_image_size を使用する。</summary>
        [JsonPropertyName("image_size")]
        public ImageSize? ImageSize { get; set; }
    }
}
