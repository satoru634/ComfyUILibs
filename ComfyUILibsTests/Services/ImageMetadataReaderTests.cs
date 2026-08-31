using System.Buffers.Binary;
using System.IO;
using System.Text;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    public class ImageMetadataReaderTests : IDisposable
    {
        private readonly ImageMetadataReader _reader = new();
        private readonly List<string> _tempFiles = new();

        public void Dispose()
        {
            foreach (var path in _tempFiles)
            {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }

        // ----- 標準的な ComfyUI t2i ワークフロー -----

        [Fact]
        public void Read_StandardComfyPrompt_ExtractsPromptsAndParameters()
        {
            const string promptJson = """
                {
                  "3": { "class_type": "KSampler", "inputs": {
                    "seed": 123456789, "steps": 28, "cfg": 6.5,
                    "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0,
                    "model": ["4", 0], "positive": ["6", 0], "negative": ["7", 0], "latent_image": ["5", 0] } },
                  "4": { "class_type": "CheckpointLoaderSimple", "inputs": { "ckpt_name": "animagineXL.safetensors" } },
                  "5": { "class_type": "EmptyLatentImage", "inputs": { "width": 832, "height": 1216, "batch_size": 1 } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "masterpiece, 1girl, solo", "clip": ["4", 1] } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "bad quality, worst quality", "clip": ["4", 1] } }
                }
                """;
            var path = WritePng(BuildPng(64, 64, ("prompt", promptJson)));

            var meta = _reader.Read(path);

            Assert.Equal(ComfyMetadataParseStatus.Ok, meta.ParseStatus);
            Assert.Equal("masterpiece, 1girl, solo", meta.PositivePrompt);
            Assert.Equal("bad quality, worst quality", meta.NegativePrompt);
            Assert.Equal("animagineXL.safetensors", meta.ModelName);
            Assert.Equal("dpmpp_2m", meta.Sampler);
            Assert.Equal("karras", meta.Scheduler);
            Assert.Equal(28, meta.Steps);
            Assert.Equal(6.5, meta.Cfg);
            Assert.Equal(123456789L, meta.Seed);
            Assert.Equal(1.0, meta.Denoise);
            Assert.Equal(832, meta.Width);
            Assert.Equal(1216, meta.Height);
            Assert.Empty(meta.Loras);
            Assert.Equal(promptJson, meta.RawPromptJson);
        }

        [Fact]
        public void Read_PromptWithLoraLoader_CollectsLoras()
        {
            const string promptJson = """
                {
                  "3": { "class_type": "KSampler", "inputs": {
                    "seed": 1, "steps": 20, "cfg": 7.0, "sampler_name": "euler", "scheduler": "normal", "denoise": 1.0,
                    "positive": ["6", 0], "negative": ["7", 0] } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "1girl" } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "bad" } },
                  "10": { "class_type": "LoraLoader", "inputs": {
                    "lora_name": "add_detail.safetensors", "strength_model": 0.8, "strength_clip": 0.7 } }
                }
                """;
            var path = WritePng(BuildPng(512, 512, ("prompt", promptJson)));

            var meta = _reader.Read(path);

            var lora = Assert.Single(meta.Loras);
            Assert.Equal("add_detail.safetensors", lora.Name);
            Assert.Equal(0.8, lora.StrengthModel);
            Assert.Equal(0.7, lora.StrengthClip);
        }

        [Fact]
        public void Read_PromptTextViaLinkedPrimitiveNode_ResolvesText()
        {
            const string promptJson = """
                {
                  "3": { "class_type": "KSampler", "inputs": { "positive": ["6", 0], "negative": ["7", 0] } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": ["20", 0] } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "negative here" } },
                  "20": { "class_type": "PrimitiveString", "inputs": { "value": "resolved positive text" } }
                }
                """;
            var path = WritePng(BuildPng(64, 64, ("prompt", promptJson)));

            var meta = _reader.Read(path);

            Assert.Equal("resolved positive text", meta.PositivePrompt);
            Assert.Equal("negative here", meta.NegativePrompt);
        }

        [Fact]
        public void Read_KSamplerAdvancedWithNoiseSeed_ReadsSeed()
        {
            const string promptJson = """
                {
                  "3": { "class_type": "KSamplerAdvanced", "inputs": {
                    "noise_seed": 987654321, "steps": 30, "cfg": 5.0,
                    "sampler_name": "euler_ancestral", "scheduler": "normal",
                    "positive": ["6", 0], "negative": ["7", 0] } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "a" } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "b" } }
                }
                """;
            var path = WritePng(BuildPng(64, 64, ("prompt", promptJson)));

            var meta = _reader.Read(path);

            Assert.Equal(987654321L, meta.Seed);
            Assert.Equal("euler_ancestral", meta.Sampler);
        }

        [Fact]
        public void Read_NoEmptyLatentImage_FallsBackToPngDimensions()
        {
            const string promptJson = """
                {
                  "3": { "class_type": "KSampler", "inputs": { "positive": ["6", 0], "negative": ["7", 0] } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "a" } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "b" } }
                }
                """;
            var path = WritePng(BuildPng(1024, 768, ("prompt", promptJson)));

            var meta = _reader.Read(path);

            Assert.Equal(1024, meta.Width);
            Assert.Equal(768, meta.Height);
        }

        // ----- 解析しきれないケース -----

        [Fact]
        public void Read_CustomSamplerWithoutResolvableLinks_ReturnsPartialWithJoinedClipTexts()
        {
            const string promptJson = """
                {
                  "1": { "class_type": "FooSampler", "inputs": { "latent": ["2", 0] } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "positive alpha" } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "positive beta" } }
                }
                """;
            var path = WritePng(BuildPng(64, 64, ("prompt", promptJson)));

            var meta = _reader.Read(path);

            Assert.Equal(ComfyMetadataParseStatus.Partial, meta.ParseStatus);
            Assert.NotNull(meta.PositivePrompt);
            Assert.Contains("positive alpha", meta.PositivePrompt);
            Assert.Contains("positive beta", meta.PositivePrompt);
            Assert.Null(meta.NegativePrompt);
        }

        [Fact]
        public void Read_WorkflowChunkOnly_ReturnsPartialAndKeepsRawJson()
        {
            const string workflowJson = """{ "nodes": [], "version": 0.4 }""";
            var path = WritePng(BuildPng(64, 64, ("workflow", workflowJson)));

            var meta = _reader.Read(path);

            Assert.Equal(ComfyMetadataParseStatus.Partial, meta.ParseStatus);
            Assert.Equal(workflowJson, meta.RawWorkflowJson);
            Assert.Null(meta.RawPromptJson);
            Assert.Null(meta.PositivePrompt);
        }

        [Fact]
        public void Read_NoTextChunks_ReturnsNoneWithPngDimensions()
        {
            var path = WritePng(BuildPng(320, 200));

            var meta = _reader.Read(path);

            Assert.Equal(ComfyMetadataParseStatus.None, meta.ParseStatus);
            Assert.Equal(320, meta.Width);
            Assert.Equal(200, meta.Height);
            Assert.Null(meta.PositivePrompt);
            Assert.Null(meta.RawPromptJson);
        }

        // ----- A1111 parameters フォールバック -----

        [Fact]
        public void Read_Automatic1111ParametersChunk_ParsesPromptsAndParameters()
        {
            const string parameters =
                "masterpiece, best quality, 1girl\n" +
                "Negative prompt: bad quality, worst quality\n" +
                "Steps: 28, Sampler: DPM++ 2M Karras, CFG scale: 7.0, Seed: 1234567890, " +
                "Size: 832x1216, Model: someCheckpoint, Denoising strength: 0.5";
            var path = WritePng(BuildPng(832, 1216, ("parameters", parameters)));

            var meta = _reader.Read(path);

            Assert.Equal(ComfyMetadataParseStatus.Ok, meta.ParseStatus);
            Assert.Equal("masterpiece, best quality, 1girl", meta.PositivePrompt);
            Assert.Equal("bad quality, worst quality", meta.NegativePrompt);
            Assert.Equal(28, meta.Steps);
            Assert.Equal("DPM++ 2M Karras", meta.Sampler);
            Assert.Equal(7.0, meta.Cfg);
            Assert.Equal(1234567890L, meta.Seed);
            Assert.Equal(832, meta.Width);
            Assert.Equal(1216, meta.Height);
            Assert.Equal("someCheckpoint", meta.ModelName);
            Assert.Equal(0.5, meta.Denoise);
        }

        [Fact]
        public void Read_PromptChunkTakesPrecedenceOverParameters()
        {
            const string promptJson = """
                {
                  "3": { "class_type": "KSampler", "inputs": { "positive": ["6", 0], "negative": ["7", 0] } },
                  "6": { "class_type": "CLIPTextEncode", "inputs": { "text": "from comfy prompt" } },
                  "7": { "class_type": "CLIPTextEncode", "inputs": { "text": "neg" } }
                }
                """;
            var path = WritePng(BuildPng(64, 64,
                ("parameters", "from a1111\nNegative prompt: x\nSteps: 10, Sampler: euler"),
                ("prompt", promptJson)));

            var meta = _reader.Read(path);

            Assert.Equal("from comfy prompt", meta.PositivePrompt);
        }

        // ----- エラー -----

        [Fact]
        public void Read_FileNotFound_ThrowsComfyUIException()
        {
            var missing = Path.Combine(Path.GetTempPath(), $"immr_missing_{Guid.NewGuid():N}.png");

            Assert.Throws<ComfyUIException>(() => _reader.Read(missing));
        }

        [Fact]
        public void Read_NonPngContent_ReturnsNone()
        {
            var path = Path.Combine(Path.GetTempPath(), $"immr_notpng_{Guid.NewGuid():N}.png");
            File.WriteAllText(path, "this is not a png file");
            _tempFiles.Add(path);

            var meta = _reader.Read(path);

            Assert.Equal(ComfyMetadataParseStatus.None, meta.ParseStatus);
        }

        // ----- PNG 生成ヘルパー -----

        private string WritePng(byte[] bytes)
        {
            var path = Path.Combine(Path.GetTempPath(), $"immr_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(path, bytes);
            _tempFiles.Add(path);
            return path;
        }

        private static byte[] BuildPng(int width, int height, params (string keyword, string text)[] texts)
        {
            using var ms = new MemoryStream();
            ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            var ihdr = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
            ihdr[8] = 8; // bit depth
            ihdr[9] = 2; // color type: truecolor
            WriteChunk(ms, "IHDR", ihdr);

            foreach (var (keyword, text) in texts)
            {
                var keywordBytes = Encoding.Latin1.GetBytes(keyword);
                var textBytes = Encoding.Latin1.GetBytes(text);
                var data = new byte[keywordBytes.Length + 1 + textBytes.Length];
                keywordBytes.CopyTo(data, 0);
                data[keywordBytes.Length] = 0;
                textBytes.CopyTo(data, keywordBytes.Length + 1);
                WriteChunk(ms, "tEXt", data);
            }

            WriteChunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            stream.Write(length);
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            stream.Write(new byte[4]); // CRC（ImageMetadataReader は検証しない）
        }
    }
}
