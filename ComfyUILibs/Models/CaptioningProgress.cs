namespace ComfyUILibs.Models
{
    /// <summary>
    /// <see cref="Services.CaptioningService.ProcessDirectoryAsync"/> における単一画像の処理結果。
    /// </summary>
    public enum CaptioningResult
    {
        /// <summary>タグ付け・.txt 書き込みに成功した。</summary>
        Processed,

        /// <summary>既存の .txt が存在し、上書き指定がなかったためスキップした。</summary>
        Skipped,

        /// <summary>画像読み込み・タグ取得・書き込みのいずれかで例外が発生した。</summary>
        Error,
    }

    /// <summary>
    /// <see cref="Services.CaptioningService.ProcessDirectoryAsync"/> が <c>IProgress&lt;CaptioningProgress&gt;</c>
    /// 経由で通知する単一画像の処理進捗。呼び出し側（GUI）はこれを購読して進捗表示・ログ出力を行う。
    /// </summary>
    /// <param name="Current">現在処理中のファイルの通し番号（1 始まり）。</param>
    /// <param name="Total">処理対象ファイルの総数。</param>
    /// <param name="FileName">処理対象ファイル名（拡張子込み、パスは含まない）。</param>
    /// <param name="Result">処理結果。</param>
    /// <param name="ErrorMessage"><see cref="CaptioningResult.Error"/> の場合の例外メッセージ。それ以外は null。</param>
    public record CaptioningProgress(
        int Current,
        int Total,
        string FileName,
        CaptioningResult Result,
        string? ErrorMessage = null);
}
