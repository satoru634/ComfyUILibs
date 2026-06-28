using System.IO;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    public class ConfigLoaderTests : IDisposable
    {
        private readonly string _tempDir;

        public ConfigLoaderTests()
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

        private string ValidConfigJson(string defaultWorkflow = "sdxl") => $$"""
            {
              "comfyui_url": "http://127.0.0.1:8188",
              "default_workflow": "{{defaultWorkflow}}",
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

        // ── ValidateImageSize ─────────────────────────────────────────────────

        [Theory]
        [InlineData(512, 512)]
        [InlineData(1024, 768)]
        [InlineData(2048, 2048)]
        public void ValidateImageSize_ValidValues_DoesNotThrow(int width, int height)
        {
            ConfigLoader.ValidateImageSize(new ImageSize { Width = width, Height = height });
        }

        [Theory]
        [InlineData(511, 512)]
        [InlineData(2049, 512)]
        [InlineData(512, 511)]
        [InlineData(512, 2049)]
        public void ValidateImageSize_OutOfRange_ThrowsComfyUIException(int width, int height)
        {
            Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidateImageSize(new ImageSize { Width = width, Height = height }));
        }

        [Theory]
        [InlineData(513, 512)]
        [InlineData(512, 513)]
        public void ValidateImageSize_NotMultipleOf8_ThrowsComfyUIException(int width, int height)
        {
            Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidateImageSize(new ImageSize { Width = width, Height = height }));
        }

        // ── LoadConfig ────────────────────────────────────────────────────────

        [Fact]
        public void LoadConfig_ValidFile_ReturnsWorkflowConfig()
        {
            var path = WriteTempFile("config.json", ValidConfigJson());

            var config = ConfigLoader.LoadConfig(path);

            Assert.Equal("http://127.0.0.1:8188", config.ComfyuiUrl);
            Assert.Equal("sdxl", config.DefaultWorkflow);
            Assert.NotNull(config.Workflows);
            Assert.True(config.Workflows.ContainsKey("sdxl"));
        }

        [Fact]
        public void LoadConfig_FileNotFound_ThrowsComfyUIException()
        {
            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.LoadConfig(Path.Combine(_tempDir, "nonexistent.json")));

            Assert.Contains("見つかりません", ex.Message);
        }

        [Fact]
        public void LoadConfig_InvalidJson_ThrowsComfyUIException()
        {
            var path = WriteTempFile("bad.json", "not json");

            Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
        }

        [Fact]
        public void LoadConfig_MissingComfyuiUrl_ThrowsComfyUIException()
        {
            var json = """{"default_workflow":"sdxl","workflows":{}}""";
            var path = WriteTempFile("config.json", json);

            var ex = Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
            Assert.Contains("comfyui_url", ex.Message);
        }

        [Fact]
        public void LoadConfig_MissingDefaultWorkflow_ThrowsComfyUIException()
        {
            var json = """{"comfyui_url":"http://localhost","workflows":{}}""";
            var path = WriteTempFile("config.json", json);

            var ex = Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
            Assert.Contains("default_workflow", ex.Message);
        }

        [Fact]
        public void LoadConfig_MissingWorkflows_ThrowsComfyUIException()
        {
            var json = """{"comfyui_url":"http://localhost","default_workflow":"sdxl"}""";
            var path = WriteTempFile("config.json", json);

            var ex = Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
            Assert.Contains("workflows", ex.Message);
        }

        [Fact]
        public void LoadConfig_DefaultWorkflowNotInWorkflows_ThrowsComfyUIException()
        {
            var path = WriteTempFile("config.json", ValidConfigJson(defaultWorkflow: "nonexistent"));

            var ex = Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
            Assert.Contains("nonexistent", ex.Message);
        }

        [Fact]
        public void LoadConfig_InvalidDefaultImageSize_ThrowsComfyUIException()
        {
            var json = """
                {
                  "comfyui_url": "http://localhost",
                  "default_workflow": "sdxl",
                  "workflows": {
                    "sdxl": {
                      "default_image_size": {"width": 100, "height": 512},
                      "image_size": {
                        "vertical": {"width": 512, "height": 768},
                        "horizontal": {"width": 768, "height": 512},
                        "square": {"width": 512, "height": 512}
                      },
                      "loras": {}
                    }
                  }
                }
                """;
            var path = WriteTempFile("config.json", json);

            Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
        }

        [Fact]
        public void LoadConfig_MissingImageSizeOrientation_ThrowsComfyUIException()
        {
            var json = """
                {
                  "comfyui_url": "http://localhost",
                  "default_workflow": "sdxl",
                  "workflows": {
                    "sdxl": {
                      "default_image_size": {"width": 512, "height": 512},
                      "image_size": {
                        "vertical": {"width": 512, "height": 768},
                        "horizontal": {"width": 768, "height": 512}
                      },
                      "loras": {}
                    }
                  }
                }
                """;
            var path = WriteTempFile("config.json", json);

            var ex = Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
            Assert.Contains("square", ex.Message);
        }

        [Fact]
        public void LoadConfig_LoraEntryMissingFile_ThrowsComfyUIException()
        {
            var json = """
                {
                  "comfyui_url": "http://localhost",
                  "default_workflow": "sdxl",
                  "workflows": {
                    "sdxl": {
                      "default_image_size": {"width": 512, "height": 512},
                      "image_size": {
                        "vertical": {"width": 512, "height": 768},
                        "horizontal": {"width": 768, "height": 512},
                        "square": {"width": 512, "height": 512}
                      },
                      "loras": {
                        "bad_lora": {"strength": 0.8}
                      }
                    }
                  }
                }
                """;
            var path = WriteTempFile("config.json", json);

            var ex = Assert.Throws<ComfyUIException>(() => ConfigLoader.LoadConfig(path));
            Assert.Contains("file", ex.Message);
        }

        // ── ValidateWd14TaggerConfig ─────────────────────────────────────────

        [Fact]
        public void ValidateWd14TaggerConfig_Valid_DoesNotThrow()
        {
            var config = new WorkflowConfig
            {
                Wd14Tagger = new Wd14TaggerConfig
                {
                    ModelName = "wd-eva02-large-tagger-v3",
                    GeneralThreshold = 0.35,
                    CharacterThreshold = 0.85
                }
            };

            ConfigLoader.ValidateWd14TaggerConfig(config);
        }

        [Fact]
        public void ValidateWd14TaggerConfig_MissingSection_ThrowsComfyUIException()
        {
            var config = new WorkflowConfig();

            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidateWd14TaggerConfig(config));
            Assert.Contains("wd14_tagger", ex.Message);
        }

        [Fact]
        public void ValidateWd14TaggerConfig_ThresholdOutOfRange_ThrowsComfyUIException()
        {
            var config = new WorkflowConfig
            {
                Wd14Tagger = new Wd14TaggerConfig
                {
                    ModelName = "model",
                    GeneralThreshold = 1.5,
                    CharacterThreshold = 0.85
                }
            };

            Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidateWd14TaggerConfig(config));
        }

        // ── LoadAndValidateInput ──────────────────────────────────────────────

        [Fact]
        public void LoadAndValidateInput_ValidFile_ReturnsWorkflowInput()
        {
            var json = """
                {
                  "loras": ["my_lora"],
                  "prompts": {"positive": "masterpiece", "negative": "bad quality"},
                  "image_size": {"width": 768, "height": 1024}
                }
                """;
            var path = WriteTempFile("input.json", json);

            var input = ConfigLoader.LoadAndValidateInput(path);

            Assert.Single(input.Loras);
            Assert.Equal("my_lora", input.Loras[0]);
            Assert.Equal("masterpiece", input.Prompts.Positive);
        }

        [Fact]
        public void LoadAndValidateInput_FileNotFound_ThrowsComfyUIException()
        {
            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.LoadAndValidateInput(Path.Combine(_tempDir, "missing.json")));
            Assert.Contains("見つかりません", ex.Message);
        }

        [Fact]
        public void LoadAndValidateInput_MissingLoras_ThrowsComfyUIException()
        {
            var path = WriteTempFile("input.json",
                """{"prompts":{"positive":"a","negative":"b"}}""");

            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.LoadAndValidateInput(path));
            Assert.Contains("loras", ex.Message);
        }

        [Fact]
        public void LoadAndValidateInput_MissingPrompts_ThrowsComfyUIException()
        {
            var path = WriteTempFile("input.json", """{"loras":[]}""");

            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.LoadAndValidateInput(path));
            Assert.Contains("prompts", ex.Message);
        }

        // ── ValidateLoras ─────────────────────────────────────────────────────

        [Fact]
        public void ValidateLoras_EmptyList_DoesNotThrow()
        {
            ConfigLoader.ValidateLoras(new List<string>());
        }

        [Fact]
        public void ValidateLoras_FourLoras_DoesNotThrow()
        {
            ConfigLoader.ValidateLoras(new List<string> { "a", "b", "c", "d" });
        }

        [Fact]
        public void ValidateLoras_FiveLoras_ThrowsComfyUIException()
        {
            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidateLoras(new List<string> { "a", "b", "c", "d", "e" }));
            Assert.Contains("最大4個", ex.Message);
        }

        [Fact]
        public void ValidateLoras_EmptyStringEntry_ThrowsComfyUIException()
        {
            Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidateLoras(new List<string> { "" }));
        }

        // ── ValidatePrompts ───────────────────────────────────────────────────

        [Fact]
        public void ValidatePrompts_NormalPrompts_DoesNotThrow()
        {
            ConfigLoader.ValidatePrompts(new PromptPair
            {
                Positive = "masterpiece",
                Negative = "bad quality"
            });
        }

        [Fact]
        public void ValidatePrompts_PositiveTooLong_ThrowsComfyUIException()
        {
            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidatePrompts(new PromptPair
                {
                    Positive = new string('a', ConfigLoader.MaxPromptLength + 1),
                    Negative = ""
                }));
            Assert.Contains("positive", ex.Message);
        }

        [Fact]
        public void ValidatePrompts_NegativeTooLong_ThrowsComfyUIException()
        {
            var ex = Assert.Throws<ComfyUIException>(() =>
                ConfigLoader.ValidatePrompts(new PromptPair
                {
                    Positive = "",
                    Negative = new string('a', ConfigLoader.MaxPromptLength + 1)
                }));
            Assert.Contains("negative", ex.Message);
        }

        // ── ValidateInputs ────────────────────────────────────────────────────

        [Fact]
        public void ValidateInputs_ValidInputWithImageSize_DoesNotThrow()
        {
            ConfigLoader.ValidateInputs(
                new List<string> { "lora1" },
                new PromptPair { Positive = "pos", Negative = "neg" },
                new ImageSize { Width = 512, Height = 768 });
        }

        [Fact]
        public void ValidateInputs_NullImageSize_DoesNotThrow()
        {
            ConfigLoader.ValidateInputs(
                new List<string>(),
                new PromptPair { Positive = "pos", Negative = "neg" },
                null);
        }
    }
}
