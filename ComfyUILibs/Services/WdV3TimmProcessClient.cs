using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Resources;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// <see cref="IWdV3TimmProcessClient"/> の既定実装。
    /// <see cref="System.Diagnostics.Process"/> で wdv3-timm を常駐サーバーモードとして起動し、
    /// 標準入出力（1 行 1 JSON）でリクエスト・応答をやり取りする。
    /// </summary>
    internal class WdV3TimmProcessClient : IWdV3TimmProcessClient
    {
        /// <summary>プロセス終了（<see cref="DisposeAsync"/>）を正常待機する上限時間。超過した場合は強制終了する。</summary>
        private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(5);

        private Process? _process;

        public async Task StartAsync(string exePath, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new ComfyUIException(Messages.Get("WdV3TimmTaggerRunner_ProcessStartFailed_Format", exePath));
            }
            catch (Win32Exception ex)
            {
                throw new ComfyUIException(Messages.Get("WdV3TimmTaggerRunner_ProcessStartFailed_Format", ex.Message), ex);
            }

            _process = process;

            // モデルロード完了の準備完了シグナル（{"status":"ready"}）を待機する。
            // ロード中の進捗ログ等はサーバー側が標準エラー出力に書く契約のため、標準出力からは
            // このシグナル行のみが読めることを前提とする。
            var readyLine = await process.StandardOutput.ReadLineAsync();
            if (!IsReady(readyLine))
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                throw new ComfyUIException(
                    Messages.Get("WdV3TimmTaggerRunner_ProcessNotReady_Format", stderr));
            }
        }

        private static bool IsReady(string? line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(line);
                return doc.RootElement.TryGetProperty("status", out var status)
                    && status.GetString() == "ready";
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public async Task<string?> SendRequestAsync(string requestJson)
        {
            if (_process == null)
                throw new InvalidOperationException("StartAsync を先に呼び出す必要があります。");

            await _process.StandardInput.WriteLineAsync(requestJson);
            await _process.StandardInput.FlushAsync();

            return await _process.StandardOutput.ReadLineAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_process == null)
                return;

            try
            {
                // 標準入力を閉じる（EOF）ことでサーバー側に終了を通知する
                _process.StandardInput.Close();

                using var cts = new CancellationTokenSource(GracefulExitTimeout);
                try
                {
                    await _process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!_process.HasExited)
                        _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 終了処理中の例外はタグ付け本体の処理結果に影響させない
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }
}
