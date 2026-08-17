namespace ComfyUILibs.Services
{
    /// <summary>
    /// wdv3-timm 常駐サーバープロセスとの標準入出力による通信を抽象化するインターフェース。
    /// <see cref="WdV3TimmProcessClient"/> が実装し、テスト時はモック実装に差し替えられる。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 想定するプロトコル（wdv3_timm.py 側の <c>--serve</c> モードとして実装される想定。
    /// 本インターフェースの実装は C# 側のクライアントに過ぎず、サーバー側の実装は本ライブラリの
    /// 対象外だが、両者はこの契約に従う必要がある）:
    /// </para>
    /// <list type="bullet">
    /// <item>起動: <c>&lt;exe_path&gt; --serve --model &lt;model&gt; -g &lt;general_threshold&gt; -c &lt;character_threshold&gt;</c></item>
    /// <item>モデルロード完了後、標準出力に 1 行だけ <c>{"status":"ready"}</c> を出力する
    /// （ロード中の進捗ログ等は標準エラー出力に書くこと。標準出力はプロトコル専用とする）。</item>
    /// <item>リクエストは標準入力へ 1 行の JSON（<c>{"image_path":"&lt;絶対パス&gt;"}</c>）を書き込み flush する。</item>
    /// <item>応答は標準出力へ 1 行の JSON で返す。成功時 <c>{"status":"ok","tags":"1girl, blue_eyes, solo"}</c>、
    /// 失敗時 <c>{"status":"error","message":"..."}</c>。</item>
    /// <item>終了: クライアントが標準入力を閉じる（EOF）とサーバーは処理中のリクエストを終えてから終了する。</item>
    /// </list>
    /// </remarks>
    public interface IWdV3TimmProcessClient : IAsyncDisposable
    {
        /// <summary>
        /// wdv3-timm を常駐サーバーモードで起動し、準備完了（<c>{"status":"ready"}</c>）を待機する。
        /// </summary>
        /// <param name="exePath">wdv3_timm.exe（または launcher）の実行ファイルパス。</param>
        /// <param name="arguments">起動時に渡すコマンドライン引数。</param>
        /// <exception cref="Exceptions.ComfyUIException">プロセス起動に失敗した場合、
        /// または準備完了状態にならなかった場合。</exception>
        Task StartAsync(string exePath, IReadOnlyList<string> arguments);

        /// <summary>
        /// 1 件のリクエスト JSON を標準入力へ送信し、標準出力から応答 JSON を 1 行受信して返す。
        /// </summary>
        /// <param name="requestJson">送信するリクエスト JSON（改行を含まない 1 行分）。</param>
        /// <returns>応答 JSON の 1 行。プロセスが予期せず終了した場合（EOF）は null。</returns>
        Task<string?> SendRequestAsync(string requestJson);
    }
}
