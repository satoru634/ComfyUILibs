using System.IO;
using System.Text;
using System.Text.Json;
using ComfyUILibs.Common;

namespace ComfyUILibsTests.Common
{
    public class JsonLoaderTests : IDisposable
    {
        private readonly string _tempDir;

        public JsonLoaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        private string TempPath(string relativePath) => Path.Combine(_tempDir, relativePath);

        private class SampleData
        {
            public string Name { get; set; } = "";
            public int Value { get; set; } = 0;
        }

        // ── ReadJson(string path) ─────────────────────────────────────────────

        [Fact]
        public void ReadJson_ByPath_ValidFile_ReturnsDeserializedObject()
        {
            var path = TempPath("sample.json");
            File.WriteAllText(path, """{"Name":"hello","Value":42}""");

            var result = JsonLoader.ReadJson<SampleData>(path);

            Assert.Equal("hello", result.Name);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void ReadJson_ByPath_FileNotFound_ThrowsFileNotFoundException()
        {
            var path = TempPath("nonexistent.json");

            Assert.Throws<FileNotFoundException>(() => JsonLoader.ReadJson<SampleData>(path));
        }

        [Fact]
        public void ReadJson_ByPath_InvalidJson_ThrowsJsonException()
        {
            var path = TempPath("invalid.json");
            File.WriteAllText(path, "not json");

            Assert.Throws<JsonException>(() => JsonLoader.ReadJson<SampleData>(path));
        }

        // ── ReadJson(Stream) ──────────────────────────────────────────────────

        [Fact]
        public void ReadJson_ByStream_ValidStream_ReturnsDeserializedObject()
        {
            var json = """{"Name":"stream","Value":7}""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var result = JsonLoader.ReadJson<SampleData>(stream);

            Assert.Equal("stream", result.Name);
            Assert.Equal(7, result.Value);
        }

        [Fact]
        public void ReadJson_ByStream_EmptyStream_ThrowsJsonException()
        {
            using var stream = new MemoryStream();

            Assert.Throws<JsonException>(() => JsonLoader.ReadJson<SampleData>(stream));
        }

        // ── DeserializeJson ───────────────────────────────────────────────────

        [Fact]
        public void DeserializeJson_ValidJson_ReturnsObject()
        {
            var json = """{"Name":"test","Value":1}""";

            var result = JsonLoader.DeserializeJson<SampleData>(json);

            Assert.Equal("test", result.Name);
            Assert.Equal(1, result.Value);
        }

        [Fact]
        public void DeserializeJson_NullInput_ReturnsNewInstance()
        {
            var result = JsonLoader.DeserializeJson<SampleData>(null);

            Assert.NotNull(result);
            Assert.Equal("", result.Name);
            Assert.Equal(0, result.Value);
        }

        [Fact]
        public void DeserializeJson_InvalidJson_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonLoader.DeserializeJson<SampleData>("invalid json"));
        }

        // ── WriteJson ─────────────────────────────────────────────────────────

        [Fact]
        public void WriteJson_ValidData_CreatesFileWithCorrectContent()
        {
            var path = TempPath("output.json");
            var data = new SampleData { Name = "write_test", Value = 99 };

            JsonLoader.WriteJson(path, data);

            Assert.True(File.Exists(path));
            var loaded = JsonLoader.ReadJson<SampleData>(path);
            Assert.Equal("write_test", loaded.Name);
            Assert.Equal(99, loaded.Value);
        }

        [Fact]
        public void WriteJson_DirectoryNotExists_CreatesDirectoryAndFile()
        {
            var path = TempPath(Path.Combine("subdir", "nested", "output.json"));
            var data = new SampleData { Name = "dir_test", Value = 1 };

            JsonLoader.WriteJson(path, data);

            Assert.True(File.Exists(path));
        }

        [Fact]
        public void WriteJson_JapaneseCharacters_NotUnicodeEscaped()
        {
            var path = TempPath("japanese.json");
            var data = new SampleData { Name = "日本語テスト", Value = 0 };

            JsonLoader.WriteJson(path, data);

            var content = File.ReadAllText(path, Encoding.UTF8);
            Assert.Contains("日本語テスト", content);
            Assert.DoesNotContain("\\u", content);
        }

        [Fact]
        public void WriteJson_OverwritesExistingFile()
        {
            var path = TempPath("overwrite.json");
            JsonLoader.WriteJson(path, new SampleData { Name = "old", Value = 1 });

            JsonLoader.WriteJson(path, new SampleData { Name = "new", Value = 2 });

            var loaded = JsonLoader.ReadJson<SampleData>(path);
            Assert.Equal("new", loaded.Name);
            Assert.Equal(2, loaded.Value);
        }

        [Fact]
        public void WriteJson_ThenReadJson_RoundTrip()
        {
            var path = TempPath("roundtrip.json");
            var original = new SampleData { Name = "round", Value = 123 };

            JsonLoader.WriteJson(path, original);
            var loaded = JsonLoader.ReadJson<SampleData>(path);

            Assert.Equal(original.Name, loaded.Name);
            Assert.Equal(original.Value, loaded.Value);
        }
    }
}
