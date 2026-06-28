using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    /// <summary>テスト用の IComfyUIClient モック（WD14 Tagger 向け）</summary>
    internal class FakeTaggerClient : IComfyUIClient
    {
        public string UploadedName { get; set; } = "uploaded.png";
        public string PromptId { get; set; } = "tagger-prompt-id";
        public string Tags { get; set; } = "1girl, solo, long hair";
        public string PreviewNodeId { get; set; } = "3";

        private JsonElement BuildHistory()
        {
            var json = $$"""
                {
                  "outputs": {
                    "{{PreviewNodeId}}": {
                      "text": ["{{Tags}}"]
                    }
                  }
                }
                """;
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        public Task<string> SubmitAsync(JsonObject workflow, string clientId)
            => Task.FromResult(PromptId);

        public Task MonitorAsync(string promptId, string clientId) => Task.CompletedTask;

        public Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png")
            => Task.FromResult(UploadedName);

        public Task<JsonElement> GetHistoryAsync(string promptId)
            => Task.FromResult(BuildHistory());

        public Task<List<OutputFile>> GetOutputsAsync(string promptId)
            => Task.FromResult(new List<OutputFile>());
    }

    public class Wd14TaggerRunnerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _templatesDir;

        public Wd14TaggerRunnerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _templatesDir = Path.Combine(_tempDir, "templates");
            Directory.CreateDirectory(_templatesDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

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

        private string CreateTemplateFile(string previewNodeId = "3")
        {
            var templateJson = $$"""
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
                  "{{previewNodeId}}": {
                    "class_type": "PreviewAny",
                    "inputs": {},
                    "_meta": {"title": "プレビュー任意"}
                  }
                }
                """;
            var path = Path.Combine(_templatesDir, "template_wd14_tagger.json");
            File.WriteAllText(path, templateJson);
            return path;
        }

        private Wd14TaggerRunner CreateRunner(IComfyUIClient? fakeClient = null, string? previewNodeId = null)
        {
            CreateTemplateFile(previewNodeId ?? "3");
            var config = CreateTaggerConfig();

            // AppDomain.CurrentDomain.BaseDirectory を一時的に上書きできないため、
            // テスト用にテンプレートを実際のベースディレクトリにも配置する
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
            Directory.CreateDirectory(basePath);
            var targetPath = Path.Combine(basePath, "template_wd14_tagger.json");
            if (!File.Exists(targetPath))
                File.Copy(Path.Combine(_templatesDir, "template_wd14_tagger.json"), targetPath);

            return new Wd14TaggerRunner(config, fakeClient ?? new FakeTaggerClient
            {
                PreviewNodeId = previewNodeId ?? "3"
            });
        }

        // ── TagAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task TagAsync_ValidInput_ReturnsTagString()
        {
            var fakeClient = new FakeTaggerClient
            {
                Tags = "1girl, solo, long hair",
                PreviewNodeId = "3"
            };
            var runner = CreateRunner(fakeClient, "3");

            var tags = await runner.TagAsync(new byte[] { 1, 2, 3 }, "test.jpg");

            Assert.Equal("1girl, solo, long hair", tags);
        }

        [Fact]
        public async Task TagAsync_NoExceptionOnValidInput_CompletesSuccessfully()
        {
            var fakeClient = new FakeTaggerClient { PreviewNodeId = "3" };
            var runner = CreateRunner(fakeClient, "3");

            // 例外が起きなければ正常に通過した
            await runner.TagAsync(new byte[] { 0 }, "photo.jpg");
        }

        [Fact]
        public async Task TagAsync_UploadFails_ThrowsComfyUIException()
        {
            var fakeClient = new ThrowingTaggerClient("アップロード失敗");
            var runner = CreateRunner(fakeClient, "3");

            var ex = await Assert.ThrowsAsync<ComfyUIException>(() =>
                runner.TagAsync(new byte[] { 0 }));
            Assert.Contains("アップロード失敗", ex.Message);
        }

        [Fact]
        public async Task TagAsync_NoTextInHistory_ThrowsComfyUIException()
        {
            var fakeClient = new EmptyHistoryTaggerClient();
            var runner = CreateRunner(fakeClient, "3");

            var ex = await Assert.ThrowsAsync<ComfyUIException>(() =>
                runner.TagAsync(new byte[] { 0 }));
            Assert.Contains("出力が取得できませんでした", ex.Message);
        }

        // ── ValidateWd14TaggerConfig ─────────────────────────────────────────

        [Fact]
        public void Constructor_MissingWd14TaggerSection_ThrowsComfyUIException()
        {
            CreateTemplateFile();
            var config = new WorkflowConfig
            {
                ComfyuiUrl = "http://localhost",
                // Wd14Tagger セクションなし
            };

            Assert.Throws<ComfyUIException>(() =>
                new Wd14TaggerRunner(config, new FakeTaggerClient()));
        }
    }

    /// <summary>Upload で例外を投げるテスト用クライアント</summary>
    internal class ThrowingTaggerClient : IComfyUIClient
    {
        private readonly string _message;
        public ThrowingTaggerClient(string message) => _message = message;

        public Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png")
            => throw new ComfyUIException(_message);

        public Task<string> SubmitAsync(JsonObject workflow, string clientId)
            => Task.FromResult("pid");
        public Task MonitorAsync(string promptId, string clientId) => Task.CompletedTask;
        public Task<JsonElement> GetHistoryAsync(string promptId)
            => Task.FromResult(JsonDocument.Parse("{}").RootElement);
        public Task<List<OutputFile>> GetOutputsAsync(string promptId)
            => Task.FromResult(new List<OutputFile>());
    }

    /// <summary>History に text がないテスト用クライアント</summary>
    internal class EmptyHistoryTaggerClient : IComfyUIClient
    {
        public Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png")
            => Task.FromResult("uploaded.png");
        public Task<string> SubmitAsync(JsonObject workflow, string clientId)
            => Task.FromResult("pid");
        public Task MonitorAsync(string promptId, string clientId) => Task.CompletedTask;
        public Task<JsonElement> GetHistoryAsync(string promptId)
            => Task.FromResult(JsonDocument.Parse("""{"outputs":{"3":{}}}""").RootElement.Clone());
        public Task<List<OutputFile>> GetOutputsAsync(string promptId)
            => Task.FromResult(new List<OutputFile>());
    }
}
