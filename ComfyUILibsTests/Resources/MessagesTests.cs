using System.Globalization;
using ComfyUILibs.Resources;

namespace ComfyUILibsTests.Resources
{
    public class MessagesTests
    {
        /// <summary>
        /// CurrentUICulture を一時的に変更するヘルパー。テスト終了時に元の値へ復元する。
        /// </summary>
        private static void WithCulture(string cultureName, Action action)
        {
            var original = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
                action();
            }
            finally
            {
                CultureInfo.CurrentUICulture = original;
            }
        }

        [Fact]
        public void Get_JapaneseCulture_ReturnsJapaneseMessage() =>
            WithCulture("ja", () =>
                Assert.Equal("設定ファイルが見つかりません", Messages.Get("ConfigLoader_ConfigFileNotFound")));

        [Fact]
        public void Get_EnglishCulture_ReturnsEnglishMessage() =>
            WithCulture("en", () =>
                Assert.Equal("Configuration file not found", Messages.Get("ConfigLoader_ConfigFileNotFound")));

        [Fact]
        public void Get_EnglishUsCulture_FallsBackToEnglishSatellite() =>
            WithCulture("en-US", () =>
                Assert.Equal("Configuration file not found", Messages.Get("ConfigLoader_ConfigFileNotFound")));

        [Fact]
        public void Get_WithFormatArgs_JapaneseCulture_FormatsMessage() =>
            WithCulture("ja", () =>
                Assert.Equal(
                    "LoRA は最大4個まで指定できます（指定数: 5）",
                    Messages.Get("ConfigLoader_TooManyLoras_Format", 5)));

        [Fact]
        public void Get_WithFormatArgs_EnglishCulture_FormatsMessage() =>
            WithCulture("en", () =>
                Assert.Equal(
                    "A maximum of 4 LoRAs can be specified (given: 5)",
                    Messages.Get("ConfigLoader_TooManyLoras_Format", 5)));

        [Fact]
        public void Get_UnknownKey_ReturnsKeyItself() =>
            Assert.Equal("NonExistentKey", Messages.Get("NonExistentKey"));
    }
}
