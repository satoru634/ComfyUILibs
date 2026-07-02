using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    /// <summary>テスト用の IComfyUIClient モック</summary>
    internal class FakeComfyUIClient : IComfyUIClient
    {
        public string PromptId { get; set; } = "test-prompt-id";
        public List<OutputFile> Outputs { get; set; } = new();
        public Exception? ThrowOnSubmit { get; set; }

        public Task<string> SubmitAsync(JsonObject workflow, string clientId)
        {
            if (ThrowOnSubmit != null)
                throw ThrowOnSubmit;
            return Task.FromResult(PromptId);
        }

        public Task MonitorAsync(string promptId, string clientId) => Task.CompletedTask;

        public Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png")
            => Task.FromResult("uploaded.png");

        public Task<System.Text.Json.JsonElement> GetHistoryAsync(string promptId)
            => Task.FromResult(JsonDocument.Parse("{}").RootElement);

        public Task<List<OutputFile>> GetOutputsAsync(string promptId)
            => Task.FromResult(Outputs);

        public Task<byte[]> GetImageAsync(string filename, string subfolder, string type)
            => Task.FromResult(Array.Empty<byte>());
    }

    public class WorkflowRunnerTests : IDisposable
    {
        private readonly string _tempDir;

        public WorkflowRunnerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        private string WriteTempFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        private string ValidConfigJson() => """
            {
              "comfyui_url": "http://127.0.0.1:8188",
              "default_workflow": "sdxl",
              "workflows": {
                "sdxl": {
                  "default_image_size": {"width": 832, "height": 1216},
                  "image_size": {
                    "vertical":   {"width": 832,  "height": 1216},
                    "horizontal": {"width": 1216, "height": 832},
                    "square":     {"width": 1024, "height": 1024}
                  },
                  "loras": {
                    "my_lora": {"file": "my_lora.safetensors", "strength": 0.8}
                  }
                }
              }
            }
            """;

        private static string MinimalTemplateJson() => """
            {
              "1": {"class_type":"CLIPTextEncode","inputs":{"text":"","seed":0},"_meta":{"title":"positive_prompt"}},
              "2": {"class_type":"CLIPTextEncode","inputs":{"text":""},"_meta":{"title":"negative_prompt"}},
              "3": {"class_type":"EmptyLatentImage","inputs":{"width":512,"height":512,"seed":0},"_meta":{"title":"empty_latent_image"}}
            }
            """;

        private WorkflowConfig LoadConfig()
            => ConfigLoader.LoadConfig(WriteTempFile("config.json", ValidConfigJson()));

        private string CreateTemplatesDir()
        {
            var dir = Path.Combine(_tempDir, "templates", "sdxl");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "template_lora_0.json"), MinimalTemplateJson());
            var lora1Template =
                """
                {
                  "1": {"class_type":"CLIPTextEncode","inputs":{"text":"","seed":0},"_meta":{"title":"positive_prompt"}},
                  "2": {"class_type":"CLIPTextEncode","inputs":{"text":""},"_meta":{"title":"negative_prompt"}},
                  "3": {"class_type":"EmptyLatentImage","inputs":{"width":512,"height":512,"seed":0},"_meta":{"title":"empty_latent_image"}},
                  "11": {"class_type":"LoraLoader","inputs":{"lora_name":"","strength_model":1.0},"_meta":{"title":"lora_loader_1"}}
                }
                """;
            File.WriteAllText(Path.Combine(dir, "template_lora_1.json"), lora1Template);
            return Path.Combine(_tempDir, "templates");
        }

        private WorkflowRunner CreateRunner(IComfyUIClient? fakeClient = null)
        {
            var config = LoadConfig();
            var templatesDir = CreateTemplatesDir();
            return new WorkflowRunner(config, "sdxl", templatesDir, fakeClient, null);
        }

        // ── コンストラクター ───────────────────────────────────────────────────

        [Fact]
        public void Constructor_UnknownWorkflow_ThrowsComfyUIException()
        {
            var config = LoadConfig();
            var ex = Assert.Throws<ComfyUIException>(() =>
                new WorkflowRunner(config, "nonexistent", _tempDir, null, null));
            Assert.Contains("nonexistent", ex.Message);
        }

        // ── GetImageSize ──────────────────────────────────────────────────────

        [Fact]
        public void GetImageSize_Vertical_ReturnsCorrectSize()
        {
            var runner = CreateRunner();
            var size = runner.GetImageSize("vertical");
            Assert.Equal(832, size.Width);
            Assert.Equal(1216, size.Height);
        }

        // ── ExecuteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_ValidInput_ReturnsOutputs()
        {
            var fakeClient = new FakeComfyUIClient
            {
                Outputs = new List<OutputFile>
                {
                    new OutputFile { Filename = "ComfyUI_00001_.png", Type = "output" }
                }
            };
            var runner = CreateRunner(fakeClient);

            var outputs = await runner.ExecuteAsync(
                new List<string>(),
                new PromptPair { Positive = "pos", Negative = "neg" });

            Assert.Single(outputs);
            Assert.Equal("ComfyUI_00001_.png", outputs[0].Filename);
        }

        [Fact]
        public async Task ExecuteAsync_WithLora_ResolvesAndSends()
        {
            var fakeClient = new FakeComfyUIClient();
            var config = LoadConfig();
            var templatesDir = CreateTemplatesDir();
            var runner = new WorkflowRunner(config, "sdxl", templatesDir, fakeClient, null);

            var outputs = await runner.ExecuteAsync(
                new List<string> { "my_lora" },
                new PromptPair { Positive = "pos", Negative = "neg" });

            Assert.Equal("test-prompt-id", runner.PromptId);
        }

        [Fact]
        public async Task ExecuteAsync_UnknownLora_ThrowsComfyUIException()
        {
            var runner = CreateRunner(new FakeComfyUIClient());

            var ex = await Assert.ThrowsAsync<ComfyUIException>(() =>
                runner.ExecuteAsync(
                    new List<string> { "nonexistent_lora" },
                    new PromptPair { Positive = "pos", Negative = "neg" }));
            Assert.Contains("nonexistent_lora", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_TooManyLoras_ThrowsComfyUIException()
        {
            var runner = CreateRunner(new FakeComfyUIClient());

            await Assert.ThrowsAsync<ComfyUIException>(() =>
                runner.ExecuteAsync(
                    new List<string> { "a", "b", "c", "d", "e" },
                    new PromptPair { Positive = "pos", Negative = "neg" }));
        }

        [Fact]
        public async Task ExecuteAsync_SetsParametersAfterSuccess()
        {
            var fakeClient = new FakeComfyUIClient();
            var runner = CreateRunner(fakeClient);

            await runner.ExecuteAsync(
                new List<string>(),
                new PromptPair { Positive = "pos text", Negative = "neg text" });

            Assert.NotNull(runner.Parameters);
            Assert.Equal("pos text", runner.Parameters!.Positive);
        }

        // ── RunAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task RunAsync_Success_WritesSuccessResult()
        {
            var fakeClient = new FakeComfyUIClient
            {
                PromptId = "pid-001",
                Outputs = new List<OutputFile>
                {
                    new OutputFile { Filename = "out.png", Type = "output" }
                }
            };
            var runner = CreateRunner(fakeClient);

            var inputPath = WriteTempFile("input.json", """
                {"loras":[],"prompts":{"positive":"pos","negative":"neg"}}
                """);
            var outputPath = Path.Combine(_tempDir, "result.json");

            await runner.RunAsync(inputPath, outputPath);

            Assert.True(File.Exists(outputPath));
            var resultJson = File.ReadAllText(outputPath);
            using var doc = JsonDocument.Parse(resultJson);
            Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("pid-001", doc.RootElement.GetProperty("prompt_id").GetString());
        }

        [Fact]
        public async Task RunAsync_ClientThrows_WritesErrorResult()
        {
            var fakeClient = new FakeComfyUIClient
            {
                ThrowOnSubmit = new ComfyUIException("接続失敗")
            };
            var runner = CreateRunner(fakeClient);

            var inputPath = WriteTempFile("input.json", """
                {"loras":[],"prompts":{"positive":"pos","negative":"neg"}}
                """);
            var outputPath = Path.Combine(_tempDir, "result.json");

            await runner.RunAsync(inputPath, outputPath);

            var resultJson = File.ReadAllText(outputPath);
            using var doc = JsonDocument.Parse(resultJson);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            Assert.Contains("接続失敗", doc.RootElement.GetProperty("error").GetString());
        }
    }
}
