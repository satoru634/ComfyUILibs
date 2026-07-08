using System.IO;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    /// <summary>テスト用の IProgress&lt;T&gt; 実装。System.Progress&lt;T&gt; と異なり同期的に記録する。</summary>
    internal class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = new();
        public void Report(T value) => Reports.Add(value);
    }

    public class CaptioningServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _templatesDir;

        public CaptioningServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _templatesDir = Path.Combine(_tempDir, "templates");
            Directory.CreateDirectory(_templatesDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        // ── テストヘルパー ────────────────────────────────────────────────

        private static WorkflowConfig CreateTaggerConfig()
            => new WorkflowConfig
            {
                ComfyuiUrl = "http://localhost:8188",
                Wd14Tagger = new Wd14TaggerConfig
                {
                    ModelName = "wd-eva02-large-tagger-v3",
                    GeneralThreshold = 0.35,
                    CharacterThreshold = 0.85
                }
            };

        /// <summary>Wd14TaggerRunner が要求する template_wd14_tagger.json を用意する（Wd14TaggerRunnerTests と同内容）。</summary>
        private void CreateTemplateFile()
        {
            var templateJson = """
                {
                  "1": {
                    "class_type": "LoadImage",
                    "inputs": {"image": ""},
                    "_meta": {"title": "画像を読み込む"}
                  },
                  "2": {
                    "class_type": "WDTimmTagger",
                    "inputs": {
                      "model_name": "",
                      "general_threshold": 0.5,
                      "character_threshold": 0.5
                    },
                    "_meta": {"title": "WD Timm Tagger"}
                  },
                  "3": {
                    "class_type": "PreviewAny",
                    "inputs": {},
                    "_meta": {"title": "プレビュー任意"}
                  }
                }
                """;
            File.WriteAllText(Path.Combine(_templatesDir, "template_wd14_tagger.json"), templateJson);

            // Wd14TaggerRunner は AppDomain.CurrentDomain.BaseDirectory/templates を参照するため、
            // テスト実行ディレクトリにも配置する（Wd14TaggerRunnerTests と同じ回避策）。
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
            Directory.CreateDirectory(basePath);
            var targetPath = Path.Combine(basePath, "template_wd14_tagger.json");
            if (!File.Exists(targetPath))
                File.Copy(Path.Combine(_templatesDir, "template_wd14_tagger.json"), targetPath);
        }

        private Wd14TaggerRunner CreateRunner(IComfyUIClient? fakeClient = null)
        {
            CreateTemplateFile();
            return new Wd14TaggerRunner(CreateTaggerConfig(), fakeClient ?? new FakeTaggerClient());
        }

        private CaptioningService CreateService(
            string tags = "1girl, solo, long hair",
            List<string>? prependTags = null,
            List<string>? excludeTags = null)
        {
            var runner = CreateRunner(new FakeTaggerClient { Tags = tags });
            return new CaptioningService(runner, prependTags, excludeTags);
        }

        private string CreateImageDir()
        {
            var dir = Path.Combine(_tempDir, "images");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ── ApplyTagFilters ──────────────────────────────────────────────

        [Fact]
        public void ApplyTagFilters_ExcludeAndPrependDedup_MatchesSpecExample()
        {
            var service = CreateService(
                prependTags: new List<string> { "my_chara", "1girl" },
                excludeTags: new List<string> { "rating:general" });

            var result = service.ApplyTagFilters("1girl, solo, long hair, rating:general");

            Assert.Equal("my_chara, 1girl, solo, long hair", result);
        }

        [Fact]
        public void ApplyTagFilters_CaseInsensitiveMatch_RemovesRegardlessOfCase()
        {
            var service = CreateService(excludeTags: new List<string> { "Rating:General" });

            var result = service.ApplyTagFilters("1girl, rating:general, solo");

            Assert.Equal("1girl, solo", result);
        }

        [Fact]
        public void ApplyTagFilters_NoPrependNoExclude_TrimsAndJoinsOnly()
        {
            var service = CreateService();

            var result = service.ApplyTagFilters(" 1girl ,solo,  long hair ");

            Assert.Equal("1girl, solo, long hair", result);
        }

        // ── ProcessDirectoryAsync ────────────────────────────────────────

        [Fact]
        public async Task ProcessDirectoryAsync_NewImage_WritesFilteredTagsAndCountsProcessed()
        {
            var dir = CreateImageDir();
            File.WriteAllBytes(Path.Combine(dir, "photo.jpg"), new byte[] { 1 });
            var service = CreateService(tags: "1girl, solo");

            var (processed, skipped, errors) = await service.ProcessDirectoryAsync(dir, recursive: false, overwrite: false);

            Assert.Equal((1, 0, 0), (processed, skipped, errors));
            Assert.Equal("1girl, solo", await File.ReadAllTextAsync(Path.Combine(dir, "photo.txt")));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_ExistingTxtWithoutOverwrite_Skips()
        {
            var dir = CreateImageDir();
            File.WriteAllBytes(Path.Combine(dir, "photo.jpg"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(dir, "photo.txt"), "既存タグ");
            var service = CreateService();

            var (processed, skipped, errors) = await service.ProcessDirectoryAsync(dir, recursive: false, overwrite: false);

            Assert.Equal((0, 1, 0), (processed, skipped, errors));
            Assert.Equal("既存タグ", await File.ReadAllTextAsync(Path.Combine(dir, "photo.txt")));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_ExistingTxtWithOverwrite_Reprocesses()
        {
            var dir = CreateImageDir();
            File.WriteAllBytes(Path.Combine(dir, "photo.jpg"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(dir, "photo.txt"), "古いタグ");
            var service = CreateService(tags: "new tag");

            var (processed, skipped, errors) = await service.ProcessDirectoryAsync(dir, recursive: false, overwrite: true);

            Assert.Equal((1, 0, 0), (processed, skipped, errors));
            Assert.Equal("new tag", await File.ReadAllTextAsync(Path.Combine(dir, "photo.txt")));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_Recursive_IncludesSubdirectoryImages()
        {
            var dir = CreateImageDir();
            var subDir = Path.Combine(dir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllBytes(Path.Combine(dir, "a.png"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(subDir, "b.png"), new byte[] { 2 });
            var service = CreateService();

            var (processed, _, _) = await service.ProcessDirectoryAsync(dir, recursive: true, overwrite: false);

            Assert.Equal(2, processed);
            Assert.True(File.Exists(Path.Combine(subDir, "b.txt")));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_NonRecursive_ExcludesSubdirectoryImages()
        {
            var dir = CreateImageDir();
            var subDir = Path.Combine(dir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllBytes(Path.Combine(dir, "a.png"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(subDir, "b.png"), new byte[] { 2 });
            var service = CreateService();

            var (processed, _, _) = await service.ProcessDirectoryAsync(dir, recursive: false, overwrite: false);

            Assert.Equal(1, processed);
            Assert.False(File.Exists(Path.Combine(subDir, "b.txt")));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_TaggerFails_CountsErrorAndContinuesProcessingOtherFiles()
        {
            var dir = CreateImageDir();
            File.WriteAllBytes(Path.Combine(dir, "a.jpg"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(dir, "b.jpg"), new byte[] { 2 });
            var runner = CreateRunner(new ThrowingTaggerClient("タグ取得失敗"));
            var service = new CaptioningService(runner);

            var (processed, skipped, errors) = await service.ProcessDirectoryAsync(dir, recursive: false, overwrite: false);

            Assert.Equal((0, 0, 2), (processed, skipped, errors));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_DirectoryNotFound_ThrowsComfyUIException()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<ComfyUIException>(
                () => service.ProcessDirectoryAsync(Path.Combine(_tempDir, "no_such_dir"), false, false));
        }

        [Fact]
        public async Task ProcessDirectoryAsync_ReportsProgressPerFile()
        {
            var dir = CreateImageDir();
            File.WriteAllBytes(Path.Combine(dir, "a.jpg"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(dir, "b.jpg"), new byte[] { 2 });
            var service = CreateService();
            var progress = new RecordingProgress<CaptioningProgress>();

            await service.ProcessDirectoryAsync(dir, recursive: false, overwrite: false, progress);

            Assert.Equal(2, progress.Reports.Count);
            Assert.Equal(1, progress.Reports[0].Current);
            Assert.Equal(2, progress.Reports[0].Total);
            Assert.Equal(CaptioningResult.Processed, progress.Reports[0].Result);
            Assert.Equal(2, progress.Reports[1].Current);
        }

        // ── GenerateReportAsync ──────────────────────────────────────────

        [Fact]
        public async Task GenerateReportAsync_SortsByCountThenAlphabetical_ExcludesReportFileItself()
        {
            var dir = CreateImageDir();
            File.WriteAllText(Path.Combine(dir, "a.txt"), "1girl, solo, blue eyes");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "1girl, solo");
            File.WriteAllText(Path.Combine(dir, "c.txt"), "1girl");
            File.WriteAllText(Path.Combine(dir, CaptioningService.ReportFileName), "stale: 999");
            var service = CreateService();

            await service.GenerateReportAsync(dir, recursive: false);

            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, CaptioningService.ReportFileName));
            Assert.Equal(new[] { "1girl: 3", "solo: 2", "blue eyes: 1" }, lines);
        }

        [Fact]
        public async Task GenerateReportAsync_Recursive_IncludesSubdirectoryTxtFiles()
        {
            var dir = CreateImageDir();
            var subDir = Path.Combine(dir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(dir, "a.txt"), "1girl");
            File.WriteAllText(Path.Combine(subDir, "b.txt"), "1girl, solo");
            var service = CreateService();

            await service.GenerateReportAsync(dir, recursive: true);

            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, CaptioningService.ReportFileName));
            Assert.Equal(new[] { "1girl: 2", "solo: 1" }, lines);
        }
    }
}
