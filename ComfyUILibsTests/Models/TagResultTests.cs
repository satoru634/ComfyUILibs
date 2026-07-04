using System.Text.Json;
using ComfyUILibs.Models;

namespace ComfyUILibsTests.Models
{
    public class TagResultTests
    {
        [Fact]
        public void DefaultValues_AreEmptyOrNull()
        {
            var result = new TagResult();

            Assert.Equal("", result.Status);
            Assert.Equal("", result.Timestamp);
            Assert.Null(result.InputFilename);
            Assert.Null(result.Tags);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Serialize_SuccessResult_ProducesExpectedJson()
        {
            var result = new TagResult
            {
                Status = "success",
                Timestamp = "2026-07-04T12:00:00",
                InputFilename = "photo.jpg",
                Tags = "1girl, solo, long hair",
                Error = null,
            };

            var json = JsonSerializer.Serialize(result);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("photo.jpg", doc.RootElement.GetProperty("input_filename").GetString());
            Assert.Equal("1girl, solo, long hair", doc.RootElement.GetProperty("tags").GetString());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("error").ValueKind);
        }

        [Fact]
        public void Deserialize_ErrorResult_RoundTripsCorrectly()
        {
            var json = """
                {
                  "status": "error",
                  "timestamp": "2026-07-04T12:00:00",
                  "input_filename": "photo.jpg",
                  "tags": null,
                  "error": "ComfyUI に接続できません"
                }
                """;

            var result = JsonSerializer.Deserialize<TagResult>(json)!;

            Assert.Equal("error", result.Status);
            Assert.Equal("photo.jpg", result.InputFilename);
            Assert.Null(result.Tags);
            Assert.Equal("ComfyUI に接続できません", result.Error);
        }
    }
}
