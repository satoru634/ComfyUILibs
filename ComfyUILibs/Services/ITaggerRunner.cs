namespace ComfyUILibs.Services
{
    /// <summary>
    /// 画像 1 枚のタグ付けを行うランナーを抽象化するインターフェース。
    /// <see cref="CaptioningService"/> はこのインターフェース経由でタグ取得を行い、
    /// バックエンド（ComfyUI 経由の <see cref="Wd14TaggerRunner"/> や、
    /// ローカルプロセス経由の <see cref="WdV3TimmTaggerRunner"/> 等）の違いを意識しない。
    /// </summary>
    public interface ITaggerRunner
    {
        /// <summary>設定ファイルの prepend_tags（全画像に共通で先頭追加するタグ）。未指定時は空リスト。</summary>
        IReadOnlyList<string> PrependTags { get; }

        /// <summary>設定ファイルの exclude_tags（全画像に共通で除外するタグ）。未指定時は空リスト。</summary>
        IReadOnlyList<string> ExcludeTags { get; }

        /// <summary>
        /// 画像バイト列に対してタグ付けを行い、タグのカンマ区切り文字列を返す。
        /// </summary>
        /// <param name="imageData">タグ付けする画像のバイト列。</param>
        /// <param name="filename">画像のファイル名（拡張子の判定等に使用する実装がある）。</param>
        /// <returns>タグのカンマ区切り文字列（例: "1girl, solo, smile"）。</returns>
        /// <exception cref="Exceptions.ComfyUIException">タグ取得に失敗した場合。</exception>
        Task<string> TagAsync(byte[] imageData, string filename = "image.png");
    }
}
