using System.Globalization;
using System.IO;
using System.Text.Json;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Resources;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// ローカルの wdv3-timm（<c>E:\Python_project\wdv3-timm</c>、timm ライブラリで WD Tagger V3 を
    /// 実行する単一スクリプト）を常駐サーバーモードで起動し、画像のタグ（カンマ区切り文字列）を
    /// 取得するクラス。ComfyUI を経由しない <see cref="ITaggerRunner"/> の実装。
    /// </summary>
    /// <remarks>
    /// wdv3_timm.py は実行のたびにモデルをロードする単発 CLI のため、画像 1 枚ごとにプロセスを
    /// 起動するとモデル再ロードのオーバーヘッドで実用的な速度が出ない。そのため本クラスは
    /// プロセスを 1 度だけ常駐起動し（初回 <see cref="TagAsync"/> 呼び出し時に遅延起動）、
    /// 標準入出力の JSON Lines プロトコル（<see cref="IWdV3TimmProcessClient"/> 参照）で
    /// 複数画像のタグ付けリクエストを処理する。サーバー側（<c>--serve</c> モード）の実装は
    /// 本ライブラリの対象外（wdv3-timm リポジトリ側の別タスク）。
    /// </remarks>
    public class WdV3TimmTaggerRunner : ITaggerRunner, IAsyncDisposable
    {
        private readonly Models.WorkflowConfig _config;
        private readonly IWdV3TimmProcessClient _processClient;
        private readonly SemaphoreSlim _startLock = new(1, 1);
        private bool _started;

        /// <summary>設定ファイルの prepend_tags（全画像に共通で先頭追加するタグ）。未指定時は空リスト。</summary>
        public IReadOnlyList<string> PrependTags => _config.PrependTags ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>設定ファイルの exclude_tags（全画像に共通で除外するタグ）。未指定時は空リスト。</summary>
        public IReadOnlyList<string> ExcludeTags => _config.ExcludeTags ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// 本番用コンストラクター。workflow_config.json を読み込んで初期化する。
        /// </summary>
        /// <param name="configPath">workflow_config.json のパス（省略時は実行ディレクトリの workflow_config.json）。</param>
        public WdV3TimmTaggerRunner(string configPath = "workflow_config.json")
            : this(ConfigLoader.LoadWdV3TimmConfig(configPath), null)
        {
        }

        /// <summary>
        /// テスト用コンストラクター。設定オブジェクトと依存を直接注入する。
        /// <c>[assembly: InternalsVisibleTo("ComfyUILibsTests")]</c> でテストプロジェクトからアクセス可能。
        /// </summary>
        /// <param name="config">読み込み済みの設定オブジェクト（wd14_tagger セクションが設定されていること。
        /// モデル名・しきい値はこのセクションを共用する）。</param>
        /// <param name="processClientOverride">テスト用プロセスクライアント。null の場合は本番実装を使用する。</param>
        internal WdV3TimmTaggerRunner(Models.WorkflowConfig config, IWdV3TimmProcessClient? processClientOverride)
        {
            _config = config;
            // wdv3_timm セクションの存在・値の検証を行う
            ConfigLoader.ValidateWdV3TimmConfig(_config);

            _processClient = processClientOverride ?? new WdV3TimmProcessClient();
        }

        /// <summary>
        /// 画像バイト列を一時ファイルへ書き出し、常駐サーバープロセスへタグ付けをリクエストして
        /// タグのカンマ区切り文字列を返す。初回呼び出し時にサーバープロセスを起動する。
        /// </summary>
        /// <param name="imageData">タグ付けする画像のバイト列。</param>
        /// <param name="filename">一時ファイルの拡張子判定に使用するファイル名（省略時は image.png）。</param>
        /// <returns>タグのカンマ区切り文字列（例: "1girl, solo, smile"）。</returns>
        /// <exception cref="ComfyUIException">プロセス起動失敗・応答不正・サーバーエラーの場合。</exception>
        public async Task<string> TagAsync(byte[] imageData, string filename = "image.png")
        {
            await EnsureStartedAsync();

            var extension = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(extension))
                extension = ".png";
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

            try
            {
                await File.WriteAllBytesAsync(tempPath, imageData);
                return await RequestTagsAsync(tempPath);
            }
            finally
            {
                try { File.Delete(tempPath); } catch { /* 一時ファイル削除の失敗は無視する */ }
            }
        }

        /// <summary>初回呼び出し時のみサーバープロセスを起動する（以降の呼び出しは何もしない）。</summary>
        private async Task EnsureStartedAsync()
        {
            if (_started)
                return;

            await _startLock.WaitAsync();
            try
            {
                if (_started)
                    return;

                // モデル名・しきい値は wd14_tagger セクションを共用する（ConfigLoader.ValidateWdV3TimmConfig で
                // Wd14Tagger の存在・model_name が WdV3TimmModelMap で変換可能であることを検証済み）。
                // 実行ファイルは captioning_config.json では指定せず、アプリ実行ファイルと同じ階層の
                // wdv3-timm フォルダに固定する（WdV3TimmPaths 参照）。存在しない場合は
                // IWdV3TimmProcessClient.StartAsync がプロセス起動失敗として ComfyUIException を送出する。
                var wd14 = _config.Wd14Tagger!;
                WdV3TimmModelMap.TryGetWdV3TimmModel(wd14.ModelName!, out var model);
                var arguments = new List<string>
                {
                    "--serve",
                    "--model", model!,
                    "-g", wd14.GeneralThreshold!.Value.ToString(CultureInfo.InvariantCulture),
                    "-c", wd14.CharacterThreshold!.Value.ToString(CultureInfo.InvariantCulture),
                };
                await _processClient.StartAsync(WdV3TimmPaths.ExeFilePath, arguments);
                _started = true;
            }
            finally
            {
                _startLock.Release();
            }
        }

        /// <summary>1 件のタグ付けリクエストを送信し、応答 JSON からタグ文字列を取り出す（アンダースコアは半角スペースへ正規化される）。</summary>
        private async Task<string> RequestTagsAsync(string imagePath)
        {
            var request = JsonSerializer.Serialize(new { image_path = imagePath });
            var responseLine = await _processClient.SendRequestAsync(request);

            if (responseLine == null)
                throw new ComfyUIException(Messages.Get("WdV3TimmTaggerRunner_ProcessExited"));

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(responseLine);
            }
            catch (JsonException ex)
            {
                throw new ComfyUIException(
                    Messages.Get("WdV3TimmTaggerRunner_ResponseParseFailed_Format", responseLine), ex);
            }

            using (doc)
            {
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

                if (status == "ok" && root.TryGetProperty("tags", out var tagsProp))
                    return NormalizeTags(tagsProp.GetString() ?? string.Empty);

                var message = root.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null;
                throw new ComfyUIException(
                    Messages.Get("WdV3TimmTaggerRunner_ServerError_Format", message ?? responseLine));
            }
        }

        /// <summary>
        /// wdv3-timm（<c>wdv3_timm.py</c> の <c>--serve</c> モード）が返すタグ文字列は、
        /// タグ名のアンダースコアを保持したまま返す仕様（プロトコル契約、<c>run_serve_mode</c> 参照）のため、
        /// ComfyUI の WD Timm Tagger カスタムノード（<see cref="Wd14TaggerRunner"/> 経由、既にアンダースコアを
        /// 半角スペースへ変換した状態で返す）とタグの見た目が揃わない。そのため本メソッドで各タグ名の
        /// アンダースコアを半角スペースへ変換する。ただし <c>^_^</c>/<c>;_;</c>/<c>&gt;_&lt;</c> のような
        /// 顔文字系タグ（4文字未満）は変換すると意味が壊れるため、WD14 Tagger 系ツールで一般的な慣習
        /// （タグ名の長さが3文字を超える場合のみ変換する）に合わせて対象外とする。
        /// </summary>
        private static string NormalizeTags(string tags)
        {
            var parts = tags.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var tag = parts[i].Trim();
                if (tag.Length > 3)
                    tag = tag.Replace('_', ' ');
                parts[i] = tag;
            }

            return string.Join(", ", parts);
        }

        /// <summary>常駐サーバープロセスを終了する（起動していない場合は何もしない）。</summary>
        public async ValueTask DisposeAsync()
        {
            if (_started)
                await _processClient.DisposeAsync();
        }
    }
}
