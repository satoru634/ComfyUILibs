using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// WD14 Tagger ワークフローを ComfyUI で実行し、画像のタグ（カンマ区切り文字列）を取得するクラス。
    /// Python 版の wd14_tagger_runner.py を移植したもの。
    /// </summary>
    public class Wd14TaggerRunner
    {
        /// <summary>テンプレート内の画像読み込みノードのタイトル。</summary>
        private const string LoadImageTitle = "画像を読み込む";

        /// <summary>テンプレート内の WD14 Tagger ノードのタイトル。</summary>
        private const string TaggerTitle = "WD Timm Tagger";

        /// <summary>タグ文字列を出力する PreviewAny ノードのタイトル。</summary>
        private const string PreviewTitle = "プレビュー任意";

        private readonly Models.WorkflowConfig _config;
        private readonly IComfyUIClient _client;

        /// <summary>起動時に読み込んだテンプレート JSON。Apply のたびに DeepCopy して使用する。</summary>
        private readonly JsonObject _template;

        /// <summary>_meta.title → ノード ID の逆引きマップ（テンプレートロード時に構築）。</summary>
        private readonly Dictionary<string, string> _titleToId;

        /// <summary>
        /// 本番用コンストラクター。config.json を読み込んで初期化する。
        /// </summary>
        /// <param name="configPath">config.json のパス（省略時は実行ディレクトリの config.json）。</param>
        public Wd14TaggerRunner(string configPath = "config.json")
            : this(ConfigLoader.LoadTaggerConfig(configPath), null)
        {
        }

        /// <summary>
        /// テスト用コンストラクター。設定オブジェクトと依存を直接注入する。
        /// <c>[assembly: InternalsVisibleTo("ComfyUILibsTests")]</c> でテストプロジェクトからアクセス可能。
        /// </summary>
        /// <param name="config">読み込み済みの設定オブジェクト（comfyui_url が設定されていること）。</param>
        /// <param name="clientOverride">テスト用 ComfyUI クライアント。null の場合は本番実装を使用する。</param>
        internal Wd14TaggerRunner(Models.WorkflowConfig config, IComfyUIClient? clientOverride)
        {
            _config = config;
            // wd14_tagger セクションの存在・値の検証を行う
            ConfigLoader.ValidateWd14TaggerConfig(_config);

            _client = clientOverride ?? new ComfyUIClient(_config.ComfyuiUrl!);
            (_template, _titleToId) = LoadTemplate();
        }

        /// <summary>
        /// WD14 Tagger テンプレート JSON を BaseDirectory/templates/ から読み込み、
        /// タイトル→ノード ID の逆引きマップを構築して返す。
        /// </summary>
        private (JsonObject template, Dictionary<string, string> titleToId) LoadTemplate()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", "template_wd14_tagger.json");
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                throw new ComfyUIException("WD14 Tagger テンプレートファイルが見つかりません");
            }

            var template = JsonNode.Parse(json)?.AsObject()
                ?? throw new ComfyUIException("WD14 Tagger テンプレートの解析に失敗しました");

            // _meta.title をキーとする逆引きマップを構築する（ノード名でのアクセスに使用）
            var titleToId = new Dictionary<string, string>();
            foreach (var kvp in template)
            {
                if (kvp.Value is JsonObject node
                    && node["_meta"] is JsonObject meta
                    && meta["title"] is JsonValue titleVal)
                {
                    titleToId[titleVal.GetValue<string>()] = kvp.Key;
                }
            }

            return (template, titleToId);
        }

        /// <summary>
        /// 画像バイト列をアップロードし、WD14 Tagger ワークフローを実行してタグ文字列を返す。
        /// </summary>
        /// <param name="imageData">タグ付けする画像のバイト列。</param>
        /// <param name="filename">アップロード時のファイル名（省略時は image.png）。</param>
        /// <returns>タグのカンマ区切り文字列（例: "1girl, solo, smile"）。</returns>
        /// <exception cref="ComfyUIException">アップロード失敗・実行エラー・タグ取得失敗の場合。</exception>
        public async Task<string> TagAsync(byte[] imageData, string filename = "image.png")
        {
            // 1. 画像をアップロードしてサーバー側のファイル名を取得する
            var uploadedName = await _client.UploadImageAsync(imageData, filename);
            // 2. テンプレートにアップロード済みファイル名と Tagger 設定を適用する
            var workflow = BuildWorkflow(uploadedName);
            // 3. ワークフローを送信して完了を待機する
            var clientId = Guid.NewGuid().ToString();
            var promptId = await _client.SubmitAsync(workflow, clientId);
            await _client.MonitorAsync(promptId, clientId);
            // 4. 履歴から PreviewAny ノードの出力テキストを取得する
            return await ExtractTagsAsync(promptId);
        }

        /// <summary>
        /// テンプレートを DeepCopy し、アップロード済み画像ファイル名と Tagger の設定値を適用した
        /// ワークフロー JSON を構築する。
        /// </summary>
        /// <param name="imageFilename">ComfyUI 側にアップロードされた画像ファイル名。</param>
        private JsonObject BuildWorkflow(string imageFilename)
        {
            // テンプレートを直接書き換えないよう DeepCopy してから処理する
            var workflow = JsonNode.Parse(_template.ToJsonString())!.AsObject();
            var wd14 = _config.Wd14Tagger!;

            // LoadImage ノードに画像ファイル名を設定する
            workflow[_titleToId[LoadImageTitle]]!["inputs"]!.AsObject()["image"]
                = JsonValue.Create(imageFilename);

            // WD Timm Tagger ノードにモデル名としきい値を設定する
            var taggerInputs = workflow[_titleToId[TaggerTitle]]!["inputs"]!.AsObject();
            taggerInputs["model_name"] = JsonValue.Create(wd14.ModelName);
            taggerInputs["general_threshold"] = JsonValue.Create(wd14.GeneralThreshold);
            taggerInputs["character_threshold"] = JsonValue.Create(wd14.CharacterThreshold);

            return workflow;
        }

        /// <summary>
        /// 実行履歴から PreviewAny ノードが出力したタグ文字列を取得する。
        /// PreviewAny ノードの outputs[previewId].text[0] がタグを保持している。
        /// </summary>
        /// <param name="promptId">タグを取得する実行の prompt_id。</param>
        /// <returns>タグのカンマ区切り文字列。</returns>
        /// <exception cref="ComfyUIException">履歴構造が不正またはタグが見つからない場合。</exception>
        private async Task<string> ExtractTagsAsync(string promptId)
        {
            var history = await _client.GetHistoryAsync(promptId);
            var previewId = _titleToId[PreviewTitle];

            if (history.TryGetProperty("outputs", out var outputs)
                && outputs.TryGetProperty(previewId, out var previewOutput)
                && previewOutput.TryGetProperty("text", out var textArray)
                && textArray.GetArrayLength() > 0)
            {
                return textArray[0].GetString()
                    ?? throw new ComfyUIException("WD Timm Tagger の出力が取得できませんでした");
            }

            throw new ComfyUIException("WD Timm Tagger の出力が取得できませんでした");
        }
    }
}
