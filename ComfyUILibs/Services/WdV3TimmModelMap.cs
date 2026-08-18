namespace ComfyUILibs.Services
{
    /// <summary>
    /// WD14 Tagger（ComfyUI の WD Timm Tagger カスタムノード、workflow_config.json の
    /// wd14_tagger.model_name）とローカル wdv3-timm（wdv3_timm.py の <c>--model</c> 引数）の
    /// モデル名対応表。両者は同じ SmilingWolf 製モデルを指すが、名前の付け方が異なる
    /// （wd14_tagger 側は Hugging Face リポジトリ名そのまま、wdv3-timm 側は
    /// <c>MODEL_REPO_MAP</c> の短縮キー）ため、この対応表で変換する。
    /// </summary>
    public static class WdV3TimmModelMap
    {
        /// <summary>wd14_tagger.model_name → wdv3-timm の --model 値。</summary>
        private static readonly IReadOnlyDictionary<string, string> Wd14ToWdV3Timm =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["wd-vit-tagger-v3"] = "vit",
                ["wd-swinv2-tagger-v3"] = "swinv2",
                ["wd-convnext-tagger-v3"] = "convnext",
                ["wd-eva02-large-tagger-v3"] = "eva02",
                ["wd-vit-large-tagger-v3"] = "vit-large",
            };

        /// <summary>
        /// wd14_tagger.model_name に対応する wdv3-timm の --model 値を取得する。
        /// </summary>
        /// <param name="wd14ModelName">wd14_tagger.model_name の値。</param>
        /// <param name="wdV3TimmModel">対応する wdv3-timm のモデルキー（見つかった場合）。</param>
        /// <returns>対応表に存在する場合は true。</returns>
        public static bool TryGetWdV3TimmModel(string wd14ModelName, out string wdV3TimmModel)
            => Wd14ToWdV3Timm.TryGetValue(wd14ModelName, out wdV3TimmModel!);

        /// <summary>対応表に含まれる wdv3-timm 側のモデルキー一覧（エラーメッセージ表示用）。</summary>
        public static IReadOnlyCollection<string> SupportedWdV3TimmModels { get; } =
            Wd14ToWdV3Timm.Values.Distinct().ToList();

        /// <summary>
        /// 対応表に含まれる wd14_tagger.model_name 一覧（GUI のモデル選択 ComboBox 等での利用を想定）。
        /// </summary>
        public static IReadOnlyCollection<string> SupportedWd14ModelNames { get; } =
            Wd14ToWdV3Timm.Keys.ToList();
    }
}
