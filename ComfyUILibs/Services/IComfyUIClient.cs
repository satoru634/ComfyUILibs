using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// ComfyUI REST API および WebSocket との通信を抽象化するインターフェース。
    /// <see cref="ComfyUIClient"/> が実装し、テスト時はモック実装に差し替えられる。
    /// </summary>
    public interface IComfyUIClient
    {
        /// <summary>
        /// ワークフロー JSON を ComfyUI に送信し、割り当てられた prompt_id を返す。
        /// </summary>
        /// <param name="workflow">送信するワークフロー JSON（<see cref="WorkflowBuilder.Apply"/> の戻り値）。</param>
        /// <param name="clientId">WebSocket 監視と紐付けるクライアント識別子（UUID 文字列）。</param>
        /// <returns>ComfyUI が割り当てた prompt_id。</returns>
        /// <exception cref="Exceptions.ComfyUIException">接続失敗・HTTP エラー時に送出。</exception>
        Task<string> SubmitAsync(JsonObject workflow, string clientId);

        /// <summary>
        /// WebSocket で実行完了またはエラーを監視し、完了まで待機する。
        /// 接続が切れた場合やバイナリフレームを受信した場合は適切に処理する。
        /// </summary>
        /// <param name="promptId">監視対象の prompt_id。</param>
        /// <param name="clientId"><see cref="SubmitAsync"/> に渡したクライアント識別子。</param>
        /// <exception cref="Exceptions.ComfyUIException">実行エラー・接続失敗・タイムアウト時に送出。</exception>
        Task MonitorAsync(string promptId, string clientId);

        /// <summary>
        /// 画像バイト列を ComfyUI にアップロードし、サーバー側で割り当てられたファイル名を返す。
        /// WD14 Tagger の入力画像を渡す際に使用する。
        /// </summary>
        /// <param name="imageData">アップロードする画像のバイト列。</param>
        /// <param name="filename">マルチパートリクエストに使用するファイル名（デフォルト: image.png）。</param>
        /// <returns>ComfyUI 側で保存されたファイル名。</returns>
        /// <exception cref="Exceptions.ComfyUIException">接続失敗・HTTP エラー時に送出。</exception>
        Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png");

        /// <summary>
        /// 指定した prompt_id の実行履歴を取得する。
        /// prompt_id が存在しない場合は空オブジェクトを返す。
        /// </summary>
        /// <param name="promptId">取得対象の prompt_id。</param>
        /// <returns>履歴 JSON の prompt_id キーに対応するオブジェクト。</returns>
        /// <exception cref="Exceptions.ComfyUIException">接続失敗・HTTP エラー時に送出。</exception>
        Task<JsonElement> GetHistoryAsync(string promptId);

        /// <summary>
        /// 指定した prompt_id のワークフロー実行で生成された出力ファイル一覧を返す。
        /// history API の outputs フィールドを走査して images を収集する。
        /// </summary>
        /// <param name="promptId">対象の prompt_id。</param>
        /// <returns>出力ファイルのリスト。生成物がない場合は空リスト。</returns>
        /// <exception cref="Exceptions.ComfyUIException">接続失敗・HTTP エラー時に送出。</exception>
        Task<List<OutputFile>> GetOutputsAsync(string promptId);

        /// <summary>
        /// GET /view で出力ファイルの実体（画像バイト列）を取得する。
        /// プレビュー表示用に <see cref="OutputFile"/> の情報からファイル本体を取得する際に使用する。
        /// </summary>
        /// <param name="filename">取得対象のファイル名。</param>
        /// <param name="subfolder">出力先のサブフォルダ名。ルート出力フォルダの場合は空文字。</param>
        /// <param name="type">ファイルの種別（例: "output"）。</param>
        /// <returns>ファイルのバイト列。</returns>
        /// <exception cref="Exceptions.ComfyUIException">接続失敗・HTTP エラー時に送出。</exception>
        Task<byte[]> GetImageAsync(string filename, string subfolder, string type);
    }
}
