using System.IO;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    public class WorkflowBuilderTests : IDisposable
    {
        private readonly string _tempDir;

        public WorkflowBuilderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        private WorkflowBuilder CreateBuilder() => new WorkflowBuilder(_tempDir);

        private string CreateTemplateDir(string workflowName)
        {
            var dir = Path.Combine(_tempDir, workflowName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void WriteTemplate(string workflowName, int loraCount, string json)
        {
            var dir = CreateTemplateDir(workflowName);
            File.WriteAllText(Path.Combine(dir, $"template_lora_{loraCount}.json"), json);
        }

        private static string MinimalTemplateJson(int loraCount = 0) => $$"""
            {
              "1": {
                "class_type": "CLIPTextEncode",
                "inputs": {"text": "original positive", "seed": 0},
                "_meta": {"title": "positive_prompt"}
              },
              "2": {
                "class_type": "CLIPTextEncode",
                "inputs": {"text": "original negative"},
                "_meta": {"title": "negative_prompt"}
              },
              "3": {
                "class_type": "EmptyLatentImage",
                "inputs": {"width": 512, "height": 512, "seed": 0},
                "_meta": {"title": "empty_latent_image"}
              }
              {{LoraNodes(loraCount)}}
            }
            """;

        private static string LoraNodes(int count)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= count; i++)
                sb.Append($$"""
                    ,
                      "{{i + 10}}": {
                        "class_type": "LoraLoader",
                        "inputs": {"lora_name": "", "strength_model": 1.0},
                        "_meta": {"title": "lora_loader_{{i}}"}
                      }
                    """);
            return sb.ToString();
        }

        // ── SelectTemplate ────────────────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(4)]
        public void SelectTemplate_ValidLoraCount_ReturnsPath(int loraCount)
        {
            WriteTemplate("sdxl", loraCount, "{}");
            var builder = CreateBuilder();

            var path = builder.SelectTemplate(loraCount, "sdxl");

            Assert.True(File.Exists(path));
            Assert.Contains($"template_lora_{loraCount}.json", path);
        }

        [Fact]
        public void SelectTemplate_WorkflowDirNotFound_ThrowsComfyUIException()
        {
            var builder = CreateBuilder();

            var ex = Assert.Throws<ComfyUIException>(() =>
                builder.SelectTemplate(0, "nonexistent"));
            Assert.Contains("テンプレートディレクトリ", ex.Message);
        }

        [Fact]
        public void SelectTemplate_TemplateFileNotFound_ThrowsComfyUIException()
        {
            CreateTemplateDir("sdxl");
            var builder = CreateBuilder();

            var ex = Assert.Throws<ComfyUIException>(() =>
                builder.SelectTemplate(3, "sdxl"));
            Assert.Contains("テンプレートファイル", ex.Message);
        }

        [Fact]
        public void SelectTemplate_LoraCountOutOfRange_ThrowsComfyUIException()
        {
            var builder = CreateBuilder();

            Assert.Throws<ComfyUIException>(() => builder.SelectTemplate(5, "sdxl"));
        }

        // ── LoadTemplate ──────────────────────────────────────────────────────

        [Fact]
        public void LoadTemplate_ValidFile_ReturnsJsonObject()
        {
            WriteTemplate("sdxl", 0, MinimalTemplateJson());
            var builder = CreateBuilder();
            var path = builder.SelectTemplate(0, "sdxl");

            var workflow = builder.LoadTemplate(path);

            Assert.NotNull(workflow);
            Assert.True(workflow.ContainsKey("1"));
        }

        [Fact]
        public void LoadTemplate_FileNotFound_ThrowsComfyUIException()
        {
            var builder = CreateBuilder();

            Assert.Throws<ComfyUIException>(() =>
                builder.LoadTemplate(Path.Combine(_tempDir, "missing.json")));
        }

        [Fact]
        public void LoadTemplate_InvalidJson_ThrowsComfyUIException()
        {
            var path = Path.Combine(_tempDir, "bad.json");
            File.WriteAllText(path, "not json");

            var builder = CreateBuilder();

            Assert.Throws<ComfyUIException>(() => builder.LoadTemplate(path));
        }

        // ── Apply: プロンプト ─────────────────────────────────────────────────

        [Fact]
        public void Apply_ReplacesPositiveAndNegativePrompts()
        {
            WriteTemplate("sdxl", 0, MinimalTemplateJson());
            var builder = CreateBuilder();
            var workflow = builder.LoadTemplate(builder.SelectTemplate(0, "sdxl"));
            var prompts = new PromptPair { Positive = "new positive", Negative = "new negative" };

            var result = builder.Apply(workflow, prompts, new List<ResolvedLora>());

            var positiveText = result["1"]!["inputs"]!["text"]!.GetValue<string>();
            var negativeText = result["2"]!["inputs"]!["text"]!.GetValue<string>();
            Assert.Equal("new positive", positiveText);
            Assert.Equal("new negative", negativeText);
        }

        [Fact]
        public void Apply_OriginalTemplateUnchanged()
        {
            WriteTemplate("sdxl", 0, MinimalTemplateJson());
            var builder = CreateBuilder();
            var workflow = builder.LoadTemplate(builder.SelectTemplate(0, "sdxl"));
            var originalText = workflow["1"]!["inputs"]!["text"]!.GetValue<string>();

            builder.Apply(workflow, new PromptPair { Positive = "changed", Negative = "changed" }, new List<ResolvedLora>());

            Assert.Equal(originalText, workflow["1"]!["inputs"]!["text"]!.GetValue<string>());
        }

        // ── Apply: 画像サイズ ─────────────────────────────────────────────────

        [Fact]
        public void Apply_WithImageSize_UpdatesWidthAndHeight()
        {
            WriteTemplate("sdxl", 0, MinimalTemplateJson());
            var builder = CreateBuilder();
            var workflow = builder.LoadTemplate(builder.SelectTemplate(0, "sdxl"));
            var imageSize = new ImageSize { Width = 768, Height = 1024 };

            var result = builder.Apply(workflow, new PromptPair(), new List<ResolvedLora>(), imageSize: imageSize);

            var width = result["3"]!["inputs"]!["width"]!.GetValue<int>();
            var height = result["3"]!["inputs"]!["height"]!.GetValue<int>();
            Assert.Equal(768, width);
            Assert.Equal(1024, height);
        }

        // ── Apply: シード ────────────────────────────────────────────────────

        [Fact]
        public void Apply_WithFixedSeed_SetsAllSeedsToSameValue()
        {
            WriteTemplate("sdxl", 0, MinimalTemplateJson());
            var builder = CreateBuilder();
            var workflow = builder.LoadTemplate(builder.SelectTemplate(0, "sdxl"));

            var result = builder.Apply(workflow, new PromptPair(), new List<ResolvedLora>(), seed: 12345L);

            var seed1 = result["1"]!["inputs"]!["seed"]!.GetValue<long>();
            var seed3 = result["3"]!["inputs"]!["seed"]!.GetValue<long>();
            Assert.Equal(12345L, seed1);
            Assert.Equal(12345L, seed3);
        }

        [Fact]
        public void Apply_WithoutSeed_GeneratesRandomSeed()
        {
            WriteTemplate("sdxl", 0, MinimalTemplateJson());
            var builder = CreateBuilder();
            var workflow1 = builder.LoadTemplate(builder.SelectTemplate(0, "sdxl"));
            var workflow2 = builder.LoadTemplate(builder.SelectTemplate(0, "sdxl"));

            var result1 = builder.Apply(workflow1, new PromptPair(), new List<ResolvedLora>());
            var result2 = builder.Apply(workflow2, new PromptPair(), new List<ResolvedLora>());

            var seed1 = result1["1"]!["inputs"]!["seed"]!.GetValue<long>();
            var seed2 = result2["1"]!["inputs"]!["seed"]!.GetValue<long>();
            // ランダム生成なので2回が等しくなる確率は極めて低い
            Assert.NotEqual(seed1, seed2);
        }

        // ── Apply: LoRA ───────────────────────────────────────────────────────

        [Fact]
        public void Apply_WithOneLora_SetsLoraNameAndStrength()
        {
            WriteTemplate("sdxl", 1, MinimalTemplateJson(loraCount: 1));
            var builder = CreateBuilder();
            var workflow = builder.LoadTemplate(builder.SelectTemplate(1, "sdxl"));
            var loras = new List<ResolvedLora>
            {
                new ResolvedLora { Name = "my_lora", File = "my_lora.safetensors", Strength = 0.8 }
            };

            var result = builder.Apply(workflow, new PromptPair(), loras);

            var loraNode = result["11"]!["inputs"]!.AsObject();
            Assert.Equal("my_lora.safetensors", loraNode["lora_name"]!.GetValue<string>());
            Assert.Equal(0.8, loraNode["strength_model"]!.GetValue<double>());
        }

        [Fact]
        public void Apply_MissingPositivePromptNode_ThrowsComfyUIException()
        {
            var json = """{"1":{"class_type":"X","inputs":{"text":""},"_meta":{"title":"negative_prompt"}}}""";
            var path = Path.Combine(_tempDir, "bad_template.json");
            File.WriteAllText(path, json);
            var builder = CreateBuilder();
            var workflow = builder.LoadTemplate(path);

            var ex = Assert.Throws<ComfyUIException>(() =>
                builder.Apply(workflow, new PromptPair(), new List<ResolvedLora>()));
            Assert.Contains("positive_prompt", ex.Message);
        }
    }
}
