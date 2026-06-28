using System.Text.Json.Serialization;

namespace ComfyUILibs.Models
{
    /// <summary>
    /// ユーザー指定の LoRA 論理名を config.json の設定で解決した結果。
    /// result.json の parameters.loras および <see cref="WorkflowBuilder"/> への入力に使用される。
    /// </summary>
    public class ResolvedLora
    {
        /// <summary>入力 JSON で指定された LoRA の論理名（config.json のキー名）。</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>config.json から解決された実ファイル名（例: my_lora.safetensors）。</summary>
        [JsonPropertyName("file")]
        public string File { get; set; } = "";

        /// <summary>config.json から解決された LoRA の適用強度。</summary>
        [JsonPropertyName("strength")]
        public double Strength { get; set; }
    }
}
