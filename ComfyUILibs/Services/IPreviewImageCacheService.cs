using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// 生成画像プレビューのローカルキャッシュを管理するインターフェース。
    /// テスト時はモック実装に差し替えられる。
    /// </summary>
    public interface IPreviewImageCacheService
    {
        /// <summary>
        /// キャッシュ済みの画像があればそのパスを返し、なければ ComfyUI から取得してキャッシュに保存する。
        /// 画像ファイルでない、または取得に失敗した場合は null を返す（例外は送出しない）。
        /// </summary>
        /// <param name="client">画像取得に使用する ComfyUI クライアント。</param>
        /// <param name="promptId">実行時の prompt_id（キャッシュファイル名の衝突回避に使用）。null 可。</param>
        /// <param name="output">取得対象の出力ファイル情報。</param>
        /// <param name="cacheDirectory">キャッシュ保存先ディレクトリ。存在しない場合は作成する。</param>
        /// <returns>キャッシュファイルのローカルパス。取得できない場合は null。</returns>
        Task<string?> GetOrFetchAsync(IComfyUIClient client, string? promptId, OutputFile output, string cacheDirectory);
    }
}
