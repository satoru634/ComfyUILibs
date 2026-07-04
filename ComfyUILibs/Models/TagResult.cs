using System.Text.Json.Serialization;

namespace ComfyUILibs.Models
{
    /// <summary>
    /// WD14 Tagger によるタグ付け結果を記録する tag_result.json のルートオブジェクト。
    /// result.json（<see cref="WorkflowResult"/>）とはスキーマが異なるため別モデルとして定義する。
    /// </summary>
    public class TagResult
    {
        /// <summary>実行結果のステータス。"success" または "error"。</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        /// <summary>実行日時（ISO 8601 形式、秒精度）。</summary>
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";

        /// <summary>タグ付け対象の入力画像ファイル名。</summary>
        [JsonPropertyName("input_filename")]
        public string? InputFilename { get; set; }

        /// <summary>タグのカンマ区切り文字列。エラー時は null。</summary>
        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        /// <summary>エラーメッセージ。成功時は null。</summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
