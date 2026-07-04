using System.IO;
using System.Text;
using System.Text.Json;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Resources;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// workflow_config.json および入力 JSON の読み込みとバリデーションを担う静的クラス。
    /// Python 版の load_files.py を移植したもの。
    /// </summary>
    public static class ConfigLoader
    {
        /// <summary>プロンプトの最大文字数。Discord ボット経由のメモリ枯渇防止のため変更・削除禁止。</summary>
        public const int MaxPromptLength = 3000;

        /// <summary>画像サイズの最小値（ピクセル）。</summary>
        public const int ImageSizeMin = 512;

        /// <summary>画像サイズの最大値（ピクセル）。</summary>
        public const int ImageSizeMax = 2048;

        /// <summary>image_size に必須の向き名。workflow_config.json の image_size キーとして要求される。</summary>
        private static readonly string[] ImageSizeOrientations = { "vertical", "horizontal", "square" };

        /// <summary>
        /// JSON デシリアライズ用オプション。プロパティ名の大文字/小文字を区別しない。
        /// [JsonPropertyName] 属性によるキーマッピングと組み合わせて使用する。
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        // ── 画像サイズバリデーション ───────────────────────────────────────────

        /// <summary>
        /// 画像サイズの幅・高さを検証する。
        /// 範囲外または 8 の倍数でない場合は <see cref="ComfyUIException"/> を送出する。
        /// </summary>
        /// <param name="imageSize">検証対象の画像サイズ。</param>
        /// <exception cref="ComfyUIException">値が 512〜2048 の範囲外、または 8 の倍数でない場合。</exception>
        public static void ValidateImageSize(ImageSize imageSize)
        {
            CheckImageSizeDimension("width", imageSize.Width);
            CheckImageSizeDimension("height", imageSize.Height);
        }

        /// <summary>幅または高さの単一次元を検証する。</summary>
        private static void CheckImageSizeDimension(string key, int val)
        {
            if (val < ImageSizeMin || val > ImageSizeMax)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_ImageSizeOutOfRange_Format", key, ImageSizeMin, ImageSizeMax, val));
            if (val % 8 != 0)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_ImageSizeNotMultipleOf8_Format", key, val));
        }

        // ── LoRA エントリバリデーション ───────────────────────────────────────

        /// <summary>workflow_config.json の loras セクション（名前 → エントリの辞書）を検証する。</summary>
        /// <exception cref="ComfyUIException">file が空文字、または strength が null（キー欠落）の場合。</exception>
        private static void ValidateLoraEntries(Dictionary<string, LoraEntry> loras)
        {
            foreach (var (name, entry) in loras)
            {
                if (string.IsNullOrWhiteSpace(entry.File))
                    throw new ComfyUIException(
                        Messages.Get("ConfigLoader_LoraFileEmpty_Format", name));
                // strength が null = JSON にキーが存在しない（デシリアライズ時のデフォルト 0.0 と区別）
                if (entry.Strength == null)
                    throw new ComfyUIException(
                        Messages.Get("ConfigLoader_LoraStrengthMissing_Format", name));
            }
        }

        // ── ワークフロー設定バリデーション ────────────────────────────────────

        /// <summary>
        /// workflow_config.json の workflows[name].image_size を検証する。
        /// "vertical"・"horizontal"・"square" の 3 キーがすべて存在し、値が有効な画像サイズであること。
        /// </summary>
        private static void ValidateWorkflowImageSize(string workflowName, Dictionary<string, ImageSize> imageSizeMap)
        {
            foreach (var orientation in ImageSizeOrientations)
            {
                if (!imageSizeMap.TryGetValue(orientation, out var size))
                    throw new ComfyUIException(
                        Messages.Get("ConfigLoader_ImageSizeOrientationMissing_Format", workflowName, orientation));
                try
                {
                    ValidateImageSize(size);
                }
                catch (ComfyUIException ex)
                {
                    // 元の例外をラップして、どのワークフロー・向きで失敗したかを明示する
                    throw new ComfyUIException(
                        Messages.Get("ConfigLoader_ImageSizeInvalid_Format", workflowName, orientation, ex.Message), ex);
                }
            }
        }

        /// <summary>workflow_config.json の workflows[name] エントリを検証する。</summary>
        private static void ValidateWorkflowEntry(string name, WorkflowSettings entry)
        {
            if (entry.DefaultImageSize == null)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_DefaultImageSizeMissing_Format", name));
            try
            {
                ValidateImageSize(entry.DefaultImageSize);
            }
            catch (ComfyUIException ex)
            {
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_DefaultImageSizeInvalid_Format", name, ex.Message), ex);
            }

            if (entry.ImageSize == null)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_WorkflowImageSizeMissing_Format", name));
            ValidateWorkflowImageSize(name, entry.ImageSize);

            if (entry.Loras == null)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_WorkflowLorasMissing_Format", name));
            ValidateLoraEntries(entry.Loras);
        }

        // ── WD14 Tagger 設定バリデーション ────────────────────────────────────

        /// <summary>
        /// config の wd14_tagger セクションを検証する。
        /// <see cref="Wd14TaggerRunner"/> のコンストラクターから呼ばれる。
        /// </summary>
        /// <param name="config">検証対象の設定オブジェクト。</param>
        /// <exception cref="ComfyUIException">セクション欠落・model_name 空・しきい値が範囲外の場合。</exception>
        public static void ValidateWd14TaggerConfig(WorkflowConfig config)
        {
            if (config.Wd14Tagger == null)
                throw new ComfyUIException(Messages.Get("ConfigLoader_Wd14TaggerSectionMissing"));

            var wd14 = config.Wd14Tagger;
            if (string.IsNullOrWhiteSpace(wd14.ModelName))
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_Wd14ModelNameEmpty"));

            ValidateWd14Threshold("general_threshold", wd14.GeneralThreshold);
            ValidateWd14Threshold("character_threshold", wd14.CharacterThreshold);
        }

        /// <summary>WD14 Tagger のしきい値（0.0〜1.0）を検証する。</summary>
        private static void ValidateWd14Threshold(string key, double? val)
        {
            if (val == null)
                throw new ComfyUIException(Messages.Get("ConfigLoader_Wd14ThresholdKeyMissing_Format", key));
            if (val < 0.0 || val > 1.0)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_Wd14ThresholdOutOfRange_Format", key, val));
        }

        // ── workflow_config.json ロード ────────────────────────────────────────────────

        /// <summary>
        /// WD14 Tagger 専用の workflow_config.json を読み込む。comfyui_url のみ必須とし、
        /// workflows などのワークフロー設定は検証しない。
        /// </summary>
        /// <param name="configPath">workflow_config.json のパス。</param>
        /// <returns>読み込んだ設定オブジェクト。</returns>
        /// <exception cref="ComfyUIException">ファイル欠落・JSON 不正・comfyui_url 欠落の場合。</exception>
        public static WorkflowConfig LoadTaggerConfig(string configPath)
        {
            var config = LoadAndParseConfig(configPath);

            if (string.IsNullOrWhiteSpace(config.ComfyuiUrl))
                throw new ComfyUIException(
                    config.ComfyuiUrl == null
                        ? Messages.Get("ConfigLoader_ComfyuiUrlKeyMissing")
                        : Messages.Get("ConfigLoader_ComfyuiUrlEmpty"));

            return config;
        }

        /// <summary>
        /// workflow_config.json を読み込み、すべての必須フィールドとワークフロー設定を検証する。
        /// </summary>
        /// <param name="configPath">workflow_config.json のパス。</param>
        /// <returns>検証済みの設定オブジェクト。</returns>
        /// <exception cref="ComfyUIException">ファイル欠落・JSON 不正・必須フィールド欠落・値が不正な場合。</exception>
        public static WorkflowConfig LoadConfig(string configPath)
        {
            var config = LoadAndParseConfig(configPath);

            if (string.IsNullOrWhiteSpace(config.ComfyuiUrl))
                throw new ComfyUIException(
                    config.ComfyuiUrl == null
                        ? Messages.Get("ConfigLoader_ComfyuiUrlKeyMissing")
                        : Messages.Get("ConfigLoader_ComfyuiUrlEmpty"));

            if (string.IsNullOrWhiteSpace(config.DefaultWorkflow))
                throw new ComfyUIException(
                    config.DefaultWorkflow == null
                        ? Messages.Get("ConfigLoader_DefaultWorkflowKeyMissing")
                        : Messages.Get("ConfigLoader_DefaultWorkflowEmpty"));

            if (config.Workflows == null)
                throw new ComfyUIException(Messages.Get("ConfigLoader_WorkflowsKeyMissing"));

            // 各ワークフローエントリを個別に検証する
            foreach (var (name, entry) in config.Workflows)
                ValidateWorkflowEntry(name, entry);

            // default_workflow は workflows のキーとして存在しなければならない
            if (!config.Workflows.ContainsKey(config.DefaultWorkflow!))
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_DefaultWorkflowNotFound_Format", config.DefaultWorkflow));

            return config;
        }

        /// <summary>ファイルを読み込んで <see cref="WorkflowConfig"/> にデシリアライズする共通処理。</summary>
        private static WorkflowConfig LoadAndParseConfig(string configPath)
        {
            string json;
            try
            {
                json = File.ReadAllText(configPath, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                throw new ComfyUIException(Messages.Get("ConfigLoader_ConfigFileNotFound"));
            }

            try
            {
                return JsonSerializer.Deserialize<WorkflowConfig>(json, JsonOptions)
                    ?? throw new ComfyUIException(Messages.Get("ConfigLoader_ConfigNotObject"));
            }
            catch (JsonException ex)
            {
                throw new ComfyUIException(Messages.Get("ConfigLoader_ConfigParseFailed_Format", ex.Message), ex);
            }
        }

        // ── 入力 JSON ロード ─────────────────────────────────────────────────

        /// <summary>
        /// 入力 JSON ファイルを読み込み、必須フィールドの存在を検証する。
        /// image_size は省略可能なため、値のバリデーションは <see cref="ValidateInputs"/> で行う。
        /// </summary>
        /// <param name="inputPath">入力 JSON ファイルのパス。</param>
        /// <returns>読み込んだ入力オブジェクト。</returns>
        /// <exception cref="ComfyUIException">ファイル欠落・JSON 不正・loras または prompts キー欠落の場合。</exception>
        public static WorkflowInput LoadAndValidateInput(string inputPath)
        {
            string json;
            try
            {
                json = File.ReadAllText(inputPath, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                throw new ComfyUIException(Messages.Get("ConfigLoader_InputFileNotFound"));
            }

            WorkflowInput input;
            try
            {
                input = JsonSerializer.Deserialize<WorkflowInput>(json, JsonOptions)
                    ?? throw new ComfyUIException(Messages.Get("ConfigLoader_InputNotObject"));
            }
            catch (JsonException ex)
            {
                throw new ComfyUIException(Messages.Get("ConfigLoader_InputParseFailed_Format", ex.Message), ex);
            }

            // デシリアライズでは「キー欠落」と「空配列/デフォルト値」を区別できないため、
            // JsonDocument で元 JSON を確認して必須キーの存在を検証する
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("loras", out _))
                throw new ComfyUIException(Messages.Get("ConfigLoader_InputLorasKeyMissing"));
            if (!root.TryGetProperty("prompts", out _))
                throw new ComfyUIException(Messages.Get("ConfigLoader_InputPromptsKeyMissing"));

            return input;
        }

        // ── 入力値バリデーション ───────────────────────────────────────────

        /// <summary>
        /// 入力 JSON の loras リストを検証する。
        /// 最大 4 件までであり、各エントリは空でない文字列であること。
        /// </summary>
        /// <param name="loras">LoRA 論理名のリスト。</param>
        /// <exception cref="ComfyUIException">5 件以上、または空文字が含まれる場合。</exception>
        public static void ValidateLoras(List<string> loras)
        {
            if (loras.Count > 4)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_TooManyLoras_Format", loras.Count));
            for (int i = 0; i < loras.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(loras[i]))
                    throw new ComfyUIException(Messages.Get("ConfigLoader_LoraEntryEmpty_Format", i));
            }
        }

        /// <summary>
        /// 入力 JSON の prompts フィールドを検証する。
        /// 各プロンプトが <see cref="MaxPromptLength"/> 以下であること。
        /// </summary>
        /// <param name="prompts">検証対象のプロンプトペア。</param>
        /// <exception cref="ComfyUIException">どちらかのプロンプトが最大文字数を超える場合。</exception>
        public static void ValidatePrompts(PromptPair prompts)
        {
            if (prompts.Positive.Length > MaxPromptLength)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_PositivePromptTooLong_Format", MaxPromptLength));
            if (prompts.Negative.Length > MaxPromptLength)
                throw new ComfyUIException(
                    Messages.Get("ConfigLoader_NegativePromptTooLong_Format", MaxPromptLength));
        }

        /// <summary>
        /// loras・prompts・imageSize をまとめて検証する。
        /// <see cref="WorkflowRunner.ExecuteAsync"/> から呼ばれる。
        /// </summary>
        /// <param name="loras">LoRA 論理名のリスト。</param>
        /// <param name="prompts">正・負プロンプトのペア。</param>
        /// <param name="imageSize">画像サイズ。null の場合はバリデーションをスキップする。</param>
        /// <exception cref="ComfyUIException">いずれかの入力が不正な場合。</exception>
        public static void ValidateInputs(List<string> loras, PromptPair prompts, ImageSize? imageSize)
        {
            ValidateLoras(loras);
            ValidatePrompts(prompts);
            if (imageSize != null)
                ValidateImageSize(imageSize);
        }
    }
}
