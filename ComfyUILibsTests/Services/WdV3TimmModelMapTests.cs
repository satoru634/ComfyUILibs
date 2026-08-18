using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    public class WdV3TimmModelMapTests
    {
        [Theory]
        [InlineData("wd-vit-tagger-v3", "vit")]
        [InlineData("wd-swinv2-tagger-v3", "swinv2")]
        [InlineData("wd-convnext-tagger-v3", "convnext")]
        [InlineData("wd-eva02-large-tagger-v3", "eva02")]
        [InlineData("wd-vit-large-tagger-v3", "vit-large")]
        public void TryGetWdV3TimmModel_KnownWd14ModelName_ReturnsExpectedModel(string wd14ModelName, string expected)
        {
            var found = WdV3TimmModelMap.TryGetWdV3TimmModel(wd14ModelName, out var model);

            Assert.True(found);
            Assert.Equal(expected, model);
        }

        [Fact]
        public void TryGetWdV3TimmModel_UnknownWd14ModelName_ReturnsFalse()
        {
            var found = WdV3TimmModelMap.TryGetWdV3TimmModel("wd-v1-4-moat-tagger-v2", out var model);

            Assert.False(found);
            Assert.Null(model);
        }

        [Fact]
        public void TryGetWdV3TimmModel_CaseInsensitive_ReturnsExpectedModel()
        {
            var found = WdV3TimmModelMap.TryGetWdV3TimmModel("WD-VIT-TAGGER-V3", out var model);

            Assert.True(found);
            Assert.Equal("vit", model);
        }

        [Fact]
        public void SupportedWdV3TimmModels_ContainsAllFiveModels()
        {
            Assert.Equal(5, WdV3TimmModelMap.SupportedWdV3TimmModels.Count);
            Assert.Contains("vit", WdV3TimmModelMap.SupportedWdV3TimmModels);
            Assert.Contains("swinv2", WdV3TimmModelMap.SupportedWdV3TimmModels);
            Assert.Contains("convnext", WdV3TimmModelMap.SupportedWdV3TimmModels);
            Assert.Contains("eva02", WdV3TimmModelMap.SupportedWdV3TimmModels);
            Assert.Contains("vit-large", WdV3TimmModelMap.SupportedWdV3TimmModels);
        }

        [Fact]
        public void SupportedWd14ModelNames_ContainsAllFiveModelNames()
        {
            Assert.Equal(5, WdV3TimmModelMap.SupportedWd14ModelNames.Count);
            Assert.Contains("wd-vit-tagger-v3", WdV3TimmModelMap.SupportedWd14ModelNames);
            Assert.Contains("wd-swinv2-tagger-v3", WdV3TimmModelMap.SupportedWd14ModelNames);
            Assert.Contains("wd-convnext-tagger-v3", WdV3TimmModelMap.SupportedWd14ModelNames);
            Assert.Contains("wd-eva02-large-tagger-v3", WdV3TimmModelMap.SupportedWd14ModelNames);
            Assert.Contains("wd-vit-large-tagger-v3", WdV3TimmModelMap.SupportedWd14ModelNames);
        }
    }
}
