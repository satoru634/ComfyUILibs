using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    public class PromptTagExtractorTests
    {
        private readonly PromptTagExtractor _extractor = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(", , ,")]
        public void ExtractTags_NullOrEmptyOrOnlySeparators_ReturnsEmpty(string? prompt)
        {
            Assert.Empty(_extractor.ExtractTags(prompt));
        }

        [Fact]
        public void ExtractTags_CommaSeparated_TrimsAndPreservesOrder()
        {
            var result = _extractor.ExtractTags("1girl, solo,  smile ");

            Assert.Equal(new[] { "1girl", "solo", "smile" }, result);
        }

        [Fact]
        public void ExtractTags_DropsEmptyPieces()
        {
            var result = _extractor.ExtractTags("1girl, , ,solo,");

            Assert.Equal(new[] { "1girl", "solo" }, result);
        }

        [Fact]
        public void ExtractTags_StripsWeightedEmphasis()
        {
            var result = _extractor.ExtractTags("(best quality:1.2), masterpiece, (worst:0.5)");

            Assert.Equal(new[] { "best quality", "masterpiece", "worst" }, result);
        }

        [Fact]
        public void ExtractTags_StripsNestedParenAndBracketEmphasis()
        {
            var result = _extractor.ExtractTags("((detailed background)), [blurry], [[dark]]");

            Assert.Equal(new[] { "detailed background", "blurry", "dark" }, result);
        }

        [Fact]
        public void ExtractTags_RemovesLoraTokens()
        {
            var result = _extractor.ExtractTags("1girl, <lora:add_detail:0.8>, solo, <lyco:foo:1:1>");

            Assert.Equal(new[] { "1girl", "solo" }, result);
        }

        [Fact]
        public void ExtractTags_LoraOnly_ReturnsEmpty()
        {
            Assert.Empty(_extractor.ExtractTags("<lora:foo:1>"));
        }

        [Fact]
        public void ExtractTags_KeepsEmbeddingTokens()
        {
            var result = _extractor.ExtractTags("1girl, embedding:badhandv4, solo");

            Assert.Equal(new[] { "1girl", "embedding:badhandv4", "solo" }, result);
        }

        [Fact]
        public void ExtractTags_RemovesStandaloneBreakToken()
        {
            var result = _extractor.ExtractTags("1girl, BREAK, solo");

            Assert.Equal(new[] { "1girl", "solo" }, result);
        }

        [Fact]
        public void ExtractTags_DeduplicatesCaseInsensitively_KeepsFirstSpelling()
        {
            var result = _extractor.ExtractTags("1girl, 1Girl, SOLO, solo");

            Assert.Equal(new[] { "1girl", "SOLO" }, result);
        }

        [Fact]
        public void ExtractTags_UnescapesParentheses_WithoutTreatingThemAsEmphasis()
        {
            var result = _extractor.ExtractTags(@"hatsune miku \(cosplay\), 1girl");

            Assert.Equal(new[] { "hatsune miku (cosplay)", "1girl" }, result);
        }

        [Fact]
        public void ExtractTags_StripsEmphasisAroundEscapedParenTag()
        {
            var result = _extractor.ExtractTags(@"(character \(series\):1.3), solo");

            Assert.Equal(new[] { "character (series)", "solo" }, result);
        }

        [Fact]
        public void ExtractTags_CollapsesInternalWhitespace()
        {
            var result = _extractor.ExtractTags("long   hair,\nblue\teyes");

            Assert.Equal(new[] { "long hair", "blue eyes" }, result);
        }
    }
}
