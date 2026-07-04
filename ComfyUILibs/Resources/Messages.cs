using System.Globalization;
using System.Resources;

namespace ComfyUILibs.Resources
{
    /// <summary>
    /// <see cref="Exceptions.ComfyUIException"/> 等のユーザー向けメッセージを
    /// <see cref="CultureInfo.CurrentUICulture"/> に応じて解決する静的ヘルパー。
    /// 既定（neutral resource, Messages.resx）は日本語、英語は Messages.en.resx。
    /// 呼び出し側（GUI）が CurrentUICulture を切り替えることで、以降にスローされる
    /// 例外メッセージの言語も自動的に切り替わる。
    /// </summary>
    internal static class Messages
    {
        private static readonly ResourceManager ResourceManager =
            new("ComfyUILibs.Resources.Messages", typeof(Messages).Assembly);

        /// <summary>指定キーのメッセージを現在の UI カルチャで取得する。</summary>
        public static string Get(string key) =>
            ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        /// <summary>書式付きメッセージを現在の UI カルチャで取得し、引数を埋め込む。</summary>
        public static string Get(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentUICulture, Get(key), args);
    }
}
