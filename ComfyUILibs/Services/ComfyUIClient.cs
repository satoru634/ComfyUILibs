using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// ComfyUI REST API および WebSocket との通信を実装するクライアントクラス。
    /// Python 版の comfyui_client.py を移植したもの。
    /// <see cref="IComfyUIClient"/> を実装し、<see cref="WorkflowRunner"/> から DI 経由で利用される。
    /// </summary>
    public class ComfyUIClient : IComfyUIClient
    {
        private readonly string _url;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// ComfyUI のベース URL と、オプションで外部から注入する <see cref="HttpClient"/> を受け取るコンストラクター。
        /// テスト時は <see cref="HttpClient"/> に <c>FakeHttpMessageHandler</c> を差し込むことでモック可能。
        /// </summary>
        /// <param name="comfyuiUrl">ComfyUI の URL（例: http://127.0.0.1:8188）。末尾スラッシュは自動除去。</param>
        /// <param name="httpClient">注入する HttpClient。null の場合はタイムアウト 30 秒のインスタンスを生成する。</param>
        public ComfyUIClient(string comfyuiUrl, HttpClient? httpClient = null)
        {
            _url = comfyuiUrl.TrimEnd('/');
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // ── ワークフロー送信 ───────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<string> SubmitAsync(JsonObject workflow, string clientId)
        {
            // ComfyUI は {"prompt": {...}, "client_id": "..."} の形式を要求する
            var payload = new JsonObject
            {
                ["prompt"] = JsonNode.Parse(workflow.ToJsonString()),
                ["client_id"] = clientId
            };
            var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

            var response = await SendAsync(() => _httpClient.PostAsync($"{_url}/prompt", content));
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonNode.Parse(responseJson);
            return result?["prompt_id"]?.GetValue<string>()
                ?? throw new ComfyUIException("ComfyUI からの応答に prompt_id がありません");
        }

        // ── WebSocket 監視 ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task MonitorAsync(string promptId, string clientId)
        {
            // http/https を ws/wss に変換して WebSocket URL を構築する
            var wsUrl = _url.Replace("http://", "ws://").Replace("https://", "wss://");
            wsUrl = $"{wsUrl}/ws?clientId={clientId}";

            var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
            }
            catch (WebSocketException ex)
            {
                ws.Dispose();
                throw new ComfyUIException($"WebSocket 接続エラー: {ex.Message}", ex);
            }

            using (ws)
            {
                await MonitorWebSocketAsync(ws, promptId);
            }
        }

        /// <summary>
        /// WebSocket メッセージを受信ループで処理し、実行完了またはエラーを検出する。
        /// 古い ComfyUI が送信する <c>executing{node:null}</c> を受信した場合はポーリングにフォールバックする。
        /// </summary>
        private async Task MonitorWebSocketAsync(ClientWebSocket ws, string promptId)
        {
            // 高速実行で WebSocket より先に処理が完了している場合のタイムアウト値
            var wsTimeout = TimeSpan.FromSeconds(2);

            while (true)
            {
                string messageText;
                WebSocketMessageType messageType;
                try
                {
                    using var cts = new CancellationTokenSource(wsTimeout);
                    (messageType, messageText) = await ReceiveMessageAsync(ws, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // タイムアウト: history で完了済みか確認（高速実行時の競合状態対策）
                    if (await IsCompletedAsync(promptId))
                        return;
                    continue;
                }
                catch (WebSocketException)
                {
                    // サーバー側が接続を突然切断した場合（Close フレームなし）は
                    // Python 版の StopAsyncIteration 相当として扱い、完了済みならエラーにしない
                    if (await IsCompletedAsync(promptId))
                        return;
                    break;
                }

                // ComfyUI はプレビュー画像をバイナリフレームで送信するためスキップする
                if (messageType == WebSocketMessageType.Binary)
                    continue;

                if (messageType == WebSocketMessageType.Close)
                    break;

                var msg = JsonNode.Parse(messageText);
                var msgType = msg?["type"]?.GetValue<string>();
                var msgData = msg?["data"] as JsonObject;

                // 他クライアントの実行メッセージは無視する
                if (msgData?["prompt_id"]?.GetValue<string>() != promptId)
                    continue;

                // 新旧 ComfyUI の完了イベントを両方受け付ける
                // execution_success: 新しい ComfyUI、execution_complete: 旧 ComfyUI
                if (msgType == "execution_complete" || msgType == "execution_success")
                    return;

                if (msgType == "execution_error")
                {
                    var errMsg = msgData?["exception_message"]?.GetValue<string>() ?? "不明なエラー";
                    throw new ComfyUIException($"ComfyUI 実行エラー: {errMsg}");
                }

                // 古い ComfyUI の完了シグナル: executing{node: null} → ポーリングへ切り替え
                if (msgType == "executing" && msgData?["node"] == null)
                    break;
            }

            // 古い ComfyUI はポーリングで完了を確認する
            await PollUntilCompletedAsync(promptId);
        }

        /// <summary>
        /// WebSocket から 1 メッセージ分のバイト列を受信し、メッセージタイプとテキストを返す。
        /// 大きなメッセージは複数フレームに分割されることがあるため、EndOfMessage まで読み続ける。
        /// </summary>
        private static async Task<(WebSocketMessageType type, string text)> ReceiveMessageAsync(
            ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[65536];
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return (result.MessageType, Encoding.UTF8.GetString(ms.ToArray()));
        }

        /// <summary>
        /// GET /history/{promptId} で完了済みか確認する。
        /// レスポンスに promptId キーが存在すれば完了とみなす。
        /// </summary>
        private async Task<bool> IsCompletedAsync(string promptId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_url}/history/{promptId}");
                if (!response.IsSuccessStatusCode)
                    return false;
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty(promptId, out _);
            }
            catch
            {
                // 接続エラー等は false として扱い、ループを継続する
                return false;
            }
        }

        /// <summary>
        /// 1 秒間隔でポーリングし、完了するまで待機する。
        /// <paramref name="timeoutSeconds"/> を超えた場合は <see cref="ComfyUIException"/> を送出する。
        /// </summary>
        private async Task PollUntilCompletedAsync(string promptId, int timeoutSeconds = 600)
        {
            for (int i = 0; i < timeoutSeconds; i++)
            {
                if (await IsCompletedAsync(promptId))
                    return;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            throw new ComfyUIException("ComfyUI の完了待機がタイムアウトしました");
        }

        // ── 画像アップロード ───────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png")
        {
            // マルチパートフォームで "image" フィールドに画像バイト列を付与する
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(imageData), "image", filename);

            var response = await SendAsync(() => _httpClient.PostAsync($"{_url}/upload/image", form));
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonNode.Parse(responseJson);
            return result?["name"]?.GetValue<string>()
                ?? throw new ComfyUIException("ComfyUI からの応答に name がありません");
        }

        // ── 履歴取得 ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<JsonElement> GetHistoryAsync(string promptId)
        {
            var response = await SendAsync(() => _httpClient.GetAsync($"{_url}/history/{promptId}"));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Clone() でドキュメントのライフタイムから切り離す（using ブロック外で使えるようにする）
            if (doc.RootElement.TryGetProperty(promptId, out var entry))
                return entry.Clone();

            return JsonDocument.Parse("{}").RootElement;
        }

        /// <inheritdoc/>
        public async Task<List<OutputFile>> GetOutputsAsync(string promptId)
        {
            var response = await SendAsync(() => _httpClient.GetAsync($"{_url}/history/{promptId}"));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var outputs = new List<OutputFile>();
            if (!doc.RootElement.TryGetProperty(promptId, out var promptEntry))
                return outputs;
            if (!promptEntry.TryGetProperty("outputs", out var nodeOutputs))
                return outputs;

            // history.outputs はノード ID をキーとする辞書形式。各ノードが images 配列を持つ
            foreach (var nodeOutput in nodeOutputs.EnumerateObject())
            {
                if (!nodeOutput.Value.TryGetProperty("images", out var images))
                    continue;
                foreach (var image in images.EnumerateArray())
                {
                    outputs.Add(new OutputFile
                    {
                        Filename = image.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "",
                        Subfolder = image.TryGetProperty("subfolder", out var sf) ? sf.GetString() ?? "" : "",
                        Type = image.TryGetProperty("type", out var tp) ? tp.GetString() ?? "" : "",
                    });
                }
            }
            return outputs;
        }

        // ── HTTP ヘルパー ─────────────────────────────────────────────────

        /// <summary>
        /// HTTP リクエストを送信し、接続エラーやタイムアウト、非成功ステータスコードを
        /// <see cref="ComfyUIException"/> に変換する共通ラッパー。
        /// </summary>
        private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> requestFunc)
        {
            HttpResponseMessage response;
            try
            {
                response = await requestFunc();
            }
            catch (HttpRequestException ex)
            {
                throw new ComfyUIException($"ComfyUI に接続できません: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new ComfyUIException("ComfyUI への接続がタイムアウトしました", ex);
            }

            if (!response.IsSuccessStatusCode)
                throw new ComfyUIException(
                    $"ComfyUI がエラーを返しました: HTTP {(int)response.StatusCode}");

            return response;
        }
    }
}
