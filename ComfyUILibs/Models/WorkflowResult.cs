using System.Text.Json.Serialization;

namespace ComfyUILibs.Models
{
    /// <summary>
    /// ComfyUI がワークフロー実行後に history API で返す出力ファイル情報。
    /// result.json の outputs 配列の各要素に対応する。
    /// </summary>
    public class OutputFile
    {
        /// <summary>出力ファイル名（例: ComfyUI_00001_.png）。</summary>
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";

        /// <summary>出力先のサブフォルダ名。ルート出力フォルダの場合は空文字。</summary>
        [JsonPropertyName("subfolder")]
        public string Subfolder { get; set; } = "";

        /// <summary>ファイルの種別（例: "output"）。ComfyUI が付与する分類文字列。</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    /// <summary>
    /// ワークフロー実行時の入力パラメーターを保持する。
    /// result.json の parameters フィールドに対応し、成功・失敗どちらの場合も記録される。
    /// </summary>
    public class WorkflowParameters
    {
        /// <summary>実行に使用したポジティブプロンプト。</summary>
        [JsonPropertyName("positive")]
        public string Positive { get; set; } = "";

        /// <summary>実行に使用したネガティブプロンプト。</summary>
        [JsonPropertyName("negative")]
        public string Negative { get; set; } = "";

        /// <summary>解決済み LoRA リスト（論理名・実ファイル名・強度）。</summary>
        [JsonPropertyName("loras")]
        public List<ResolvedLora> Loras { get; set; } = new();

        /// <summary>実際に生成に使用した画像サイズ（default_image_size が適用された後の値）。</summary>
        [JsonPropertyName("image_size")]
        public ImageSize ImageSize { get; set; } = new();
    }

    /// <summary>
    /// ワークフロー実行の成功・失敗を記録する result.json のルートオブジェクト。
    /// <see cref="WorkflowRunner.RunAsync"/> が出力する。
    /// </summary>
    public class WorkflowResult
    {
        /// <summary>実行結果のステータス。"success" または "error"。</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        /// <summary>ComfyUI が割り当てた実行 ID。エラー時は null。</summary>
        [JsonPropertyName("prompt_id")]
        public string? PromptId { get; set; }

        /// <summary>実行日時（ISO 8601 形式、秒精度）。</summary>
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";

        /// <summary>実行に使用したテンプレートファイルのパス。エラー時は null の場合がある。</summary>
        [JsonPropertyName("template")]
        public string? Template { get; set; }

        /// <summary>実行時の入力パラメーター（プロンプト・LoRA・画像サイズ）。</summary>
        [JsonPropertyName("parameters")]
        public WorkflowParameters Parameters { get; set; } = new();

        /// <summary>生成された出力ファイルのリスト。エラー時は空リスト。</summary>
        [JsonPropertyName("outputs")]
        public List<OutputFile> Outputs { get; set; } = new();

        /// <summary>エラーメッセージ。成功時は null。</summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
