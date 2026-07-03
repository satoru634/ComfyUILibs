using System.IO;
using System.Text.Json.Nodes;
using ComfyUILibs.Common;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// ワークフロー実行の全工程（設定ロード → 入力バリデーション → テンプレート構築 →
    /// ComfyUI 送信 → 完了監視 → 結果取得）を統括するファサードクラス。
    /// Python 版の WorkflowRunner クラスを移植したもの。
    /// </summary>
    public class WorkflowRunner
    {
        private readonly WorkflowConfig _config;
        private readonly WorkflowSettings _workflowSettings;
        private readonly string _workflowName;
        private readonly string _templatesDir;

        /// <summary>テスト時に差し込む ComfyUI クライアント。null の場合は本番実装を使用する。</summary>
        private readonly IComfyUIClient? _clientOverride;

        /// <summary>テスト時に差し込む WorkflowBuilder。null の場合は本番実装を使用する。</summary>
        private readonly WorkflowBuilder? _builderOverride;

        /// <summary>
        /// <see cref="ComfyUIClient.MonitorAsync"/> が完了を検知した直後は、ComfyUI 側の
        /// history への反映がわずかに遅延し <c>GetOutputsAsync</c> が空リストを返すことがあるため、
        /// 空だった場合にリトライする回数。
        /// </summary>
        private const int MaxOutputsRetryCount = 3;

        /// <summary>outputs 取得リトライの間隔。</summary>
        private static readonly TimeSpan OutputsRetryDelay = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// 直前の <see cref="ExecuteAsync"/> で使用したテンプレートファイルのパス。
        /// <see cref="RunAsync"/> が result.json に書き込む際に参照する。
        /// </summary>
        public string? TemplatePath { get; private set; }

        /// <summary>
        /// 直前の <see cref="ExecuteAsync"/> で ComfyUI が割り当てた prompt_id。
        /// <see cref="RunAsync"/> が result.json に書き込む際に参照する。
        /// </summary>
        public string? PromptId { get; private set; }

        /// <summary>
        /// 直前の <see cref="ExecuteAsync"/> の実行パラメーター（プロンプト・LoRA・画像サイズ）。
        /// <see cref="RunAsync"/> が result.json に書き込む際に参照する。
        /// </summary>
        public WorkflowParameters? Parameters { get; private set; }

        /// <summary>
        /// 本番用コンストラクター。workflow_config.json を読み込んで初期化する。
        /// templates ディレクトリは実行ファイルと同階層の <c>templates/</c> を使用する。
        /// </summary>
        /// <param name="configPath">workflow_config.json のパス（省略時は実行ディレクトリの workflow_config.json）。</param>
        /// <param name="workflowName">使用するワークフロー名。null の場合は default_workflow を使用する。</param>
        public WorkflowRunner(string configPath = "workflow_config.json", string? workflowName = null)
            : this(ConfigLoader.LoadConfig(configPath), workflowName,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates"),
                null, null)
        {
        }

        /// <summary>
        /// テスト用コンストラクター。設定オブジェクトと依存を直接注入する。
        /// <c>[assembly: InternalsVisibleTo("ComfyUILibsTests")]</c> でテストプロジェクトからアクセス可能。
        /// </summary>
        /// <param name="config">検証済みの設定オブジェクト。</param>
        /// <param name="workflowName">使用するワークフロー名。null の場合は default_workflow を使用する。</param>
        /// <param name="templatesDir">テンプレートディレクトリのパス。</param>
        /// <param name="clientOverride">テスト用 ComfyUI クライアント。null の場合は本番実装を使用する。</param>
        /// <param name="builderOverride">テスト用 WorkflowBuilder。null の場合は本番実装を使用する。</param>
        internal WorkflowRunner(
            WorkflowConfig config,
            string? workflowName,
            string templatesDir,
            IComfyUIClient? clientOverride,
            WorkflowBuilder? builderOverride)
        {
            _config = config;
            _templatesDir = templatesDir;
            _clientOverride = clientOverride;
            _builderOverride = builderOverride;

            // workflowName が null の場合は default_workflow を採用する
            _workflowName = workflowName ?? _config.DefaultWorkflow!;
            if (!_config.Workflows!.ContainsKey(_workflowName))
                throw new ComfyUIException(
                    $"ワークフロー '{_workflowName}' が workflow_config.json の workflows に存在しません");

            _workflowSettings = _config.Workflows[_workflowName];
        }

        /// <summary>
        /// 指定した向き（"vertical"/"horizontal"/"square"）に対応する画像サイズを返す。
        /// GUI の向き選択 UI と workflow_config.json の image_size を橋渡しするメソッド。
        /// </summary>
        /// <param name="orientation">向き文字列（"vertical"・"horizontal"・"square"）。</param>
        /// <returns>対応する <see cref="ImageSize"/>。</returns>
        public ImageSize GetImageSize(string orientation)
            => _workflowSettings.ImageSize![orientation];

        /// <summary>
        /// LoRA・プロンプト・画像サイズを受け取り、ComfyUI にワークフローを送信して生成物のリストを返す。
        /// 実行後は <see cref="TemplatePath"/>・<see cref="PromptId"/>・<see cref="Parameters"/> が更新される。
        /// </summary>
        /// <param name="loras">LoRA 論理名のリスト（最大 4 件、workflow_config.json のキーと一致すること）。</param>
        /// <param name="prompts">正・負プロンプトのペア。</param>
        /// <param name="imageSize">画像サイズ。null の場合はワークフローの default_image_size を使用する。</param>
        /// <returns>生成された出力ファイルのリスト。</returns>
        /// <exception cref="ComfyUIException">バリデーション失敗・LoRA 解決失敗・ComfyUI エラーの場合。</exception>
        public async Task<List<OutputFile>> ExecuteAsync(
            List<string> loras,
            PromptPair prompts,
            ImageSize? imageSize = null)
        {
            // 各実行前に前回の状態をクリアする（RunAsync で例外発生時の参照を防ぐ）
            TemplatePath = null;
            PromptId = null;
            Parameters = null;

            ConfigLoader.ValidateInputs(loras, prompts, imageSize);

            // imageSize が未指定の場合はワークフロー設定のデフォルトサイズを使用する
            var effectiveSize = imageSize ?? _workflowSettings.DefaultImageSize!;
            var resolved = ResolveLoras(loras, _workflowSettings.Loras!);

            // 実行パラメーターを記録しておく（result.json 出力・エラー時の記録に使用）
            Parameters = new WorkflowParameters
            {
                Positive = prompts.Positive,
                Negative = prompts.Negative,
                Loras = resolved,
                ImageSize = effectiveSize
            };

            var builder = _builderOverride ?? new WorkflowBuilder(_templatesDir);
            var templatePath = builder.SelectTemplate(resolved.Count, _workflowName);
            var workflow = builder.LoadTemplate(templatePath);
            var builtWorkflow = builder.Apply(workflow, prompts, resolved, imageSize: effectiveSize);

            var client = _clientOverride ?? new ComfyUIClient(_config.ComfyuiUrl!);
            var clientId = Guid.NewGuid().ToString();
            var promptId = await client.SubmitAsync(builtWorkflow, clientId);
            await client.MonitorAsync(promptId, clientId);
            var outputs = await client.GetOutputsAsync(promptId);

            // 完了検知直後は history 反映が間に合わず空リストになることがあるためリトライする
            for (int attempt = 0; outputs.Count == 0 && attempt < MaxOutputsRetryCount; attempt++)
            {
                await Task.Delay(OutputsRetryDelay);
                outputs = await client.GetOutputsAsync(promptId);
            }

            // 成功時のみ状態を更新する（例外が発生すると到達しないため、失敗時は null のまま）
            TemplatePath = templatePath;
            PromptId = promptId;
            return outputs;
        }

        /// <summary>
        /// 入力 JSON を読み込んで <see cref="ExecuteAsync"/> を実行し、結果を output JSON に書き込む。
        /// <see cref="ComfyUIException"/> が発生した場合はエラー情報を記録した JSON を出力する。
        /// </summary>
        /// <param name="inputPath">入力 JSON ファイルのパス（<see cref="WorkflowInput"/> 形式）。</param>
        /// <param name="outputPath">結果 JSON の出力先パス（<see cref="WorkflowResult"/> 形式）。</param>
        public async Task RunAsync(string inputPath, string outputPath)
        {
            WorkflowResult result;
            try
            {
                var input = ConfigLoader.LoadAndValidateInput(inputPath);
                var outputs = await ExecuteAsync(input.Loras, input.Prompts, input.ImageSize);
                result = new WorkflowResult
                {
                    Status = "success",
                    PromptId = PromptId,
                    Timestamp = DateTime.Now.ToString("s"),
                    Template = TemplatePath,
                    Parameters = Parameters ?? new WorkflowParameters(),
                    Outputs = outputs,
                };
            }
            catch (ComfyUIException ex)
            {
                // エラー時も Parameters や TemplatePath を記録することで後から原因を追跡できる
                result = new WorkflowResult
                {
                    Status = "error",
                    Timestamp = DateTime.Now.ToString("s"),
                    Template = TemplatePath,
                    Parameters = Parameters ?? new WorkflowParameters(),
                    Error = ex.Message,
                };
            }

            JsonLoader.WriteJson(outputPath, result);
        }

        /// <summary>
        /// LoRA 論理名リストを workflow_config.json の設定に基づいて <see cref="ResolvedLora"/> リストに変換する。
        /// </summary>
        /// <param name="loraNames">LoRA 論理名のリスト。</param>
        /// <param name="loraList">workflow_config.json の loras 辞書。</param>
        /// <returns>解決済み LoRA エントリのリスト。</returns>
        /// <exception cref="ComfyUIException">loras にキーが存在しない LoRA 名が指定された場合。</exception>
        private static List<ResolvedLora> ResolveLoras(
            List<string> loraNames,
            Dictionary<string, LoraEntry> loraList)
        {
            var resolved = new List<ResolvedLora>();
            foreach (var name in loraNames)
            {
                if (!loraList.TryGetValue(name, out var entry))
                    throw new ComfyUIException(
                        $"LoRA '{name}' が workflow_config.json の loras 設定に存在しません");
                resolved.Add(new ResolvedLora
                {
                    Name = name,
                    File = entry.File!,
                    Strength = entry.Strength!.Value
                });
            }
            return resolved;
        }
    }
}
