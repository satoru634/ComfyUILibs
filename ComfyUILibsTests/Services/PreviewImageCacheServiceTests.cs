using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    /// <summary>テスト用の IComfyUIClient モック（画像取得のみ使用）</summary>
    internal class FakeImageClient : IComfyUIClient
    {
        public byte[] ImageBytes { get; set; } = new byte[] { 1, 2, 3 };
        public Exception? ThrowOnGetImage { get; set; }
        public int GetImageCallCount { get; private set; }

        public Task<string> SubmitAsync(JsonObject workflow, string clientId)
            => Task.FromResult("pid");

        public Task MonitorAsync(string promptId, string clientId) => Task.CompletedTask;

        public Task<string> UploadImageAsync(byte[] imageData, string filename = "image.png")
            => Task.FromResult("uploaded.png");

        public Task<JsonElement> GetHistoryAsync(string promptId)
            => Task.FromResult(JsonDocument.Parse("{}").RootElement);

        public Task<List<OutputFile>> GetOutputsAsync(string promptId)
            => Task.FromResult(new List<OutputFile>());

        public Task<byte[]> GetImageAsync(string filename, string subfolder, string type)
        {
            GetImageCallCount++;
            if (ThrowOnGetImage != null)
                throw ThrowOnGetImage;
            return Task.FromResult(ImageBytes);
        }
    }

    public class PreviewImageCacheServiceTests : IDisposable
    {
        private readonly string _cacheDir;

        public PreviewImageCacheServiceTests()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        public void Dispose()
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
        }

        // ── IsImageFile ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("ComfyUI_00001_.png", true)]
        [InlineData("photo.JPG", true)]
        [InlineData("photo.jpeg", true)]
        [InlineData("photo.webp", true)]
        [InlineData("video.mp4", false)]
        [InlineData("data.json", false)]
        public void IsImageFile_JudgesByExtension(string filename, bool expected)
        {
            Assert.Equal(expected, PreviewImageCacheService.IsImageFile(filename));
        }

        // ── GetOrFetchAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetOrFetchAsync_NonImageFile_ReturnsNull()
        {
            var service = new PreviewImageCacheService();
            var client = new FakeImageClient();
            var output = new OutputFile { Filename = "video.mp4", Subfolder = "", Type = "output" };

            var path = await service.GetOrFetchAsync(client, "pid", output, _cacheDir);

            Assert.Null(path);
            Assert.Equal(0, client.GetImageCallCount);
        }

        [Fact]
        public async Task GetOrFetchAsync_NotCached_FetchesAndSavesToCache()
        {
            var service = new PreviewImageCacheService();
            var client = new FakeImageClient { ImageBytes = new byte[] { 9, 9, 9 } };
            var output = new OutputFile { Filename = "img.png", Subfolder = "", Type = "output" };

            var path = await service.GetOrFetchAsync(client, "pid-1", output, _cacheDir);

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.Equal(new byte[] { 9, 9, 9 }, await File.ReadAllBytesAsync(path!));
            Assert.Equal(1, client.GetImageCallCount);
        }

        [Fact]
        public async Task GetOrFetchAsync_AlreadyCached_DoesNotCallClient()
        {
            var service = new PreviewImageCacheService();
            var client = new FakeImageClient();
            var output = new OutputFile { Filename = "img.png", Subfolder = "", Type = "output" };

            var firstPath = await service.GetOrFetchAsync(client, "pid-1", output, _cacheDir);
            var secondPath = await service.GetOrFetchAsync(client, "pid-1", output, _cacheDir);

            Assert.Equal(firstPath, secondPath);
            Assert.Equal(1, client.GetImageCallCount);
        }

        [Fact]
        public async Task GetOrFetchAsync_FetchFails_ReturnsNull()
        {
            var service = new PreviewImageCacheService();
            var client = new FakeImageClient { ThrowOnGetImage = new ComfyUIException("接続失敗") };
            var output = new OutputFile { Filename = "img.png", Subfolder = "", Type = "output" };

            var path = await service.GetOrFetchAsync(client, "pid-1", output, _cacheDir);

            Assert.Null(path);
        }

        [Fact]
        public async Task GetOrFetchAsync_SamePromptDifferentSubfolder_DoesNotCollide()
        {
            var service = new PreviewImageCacheService();
            var client = new FakeImageClient { ImageBytes = new byte[] { 1 } };
            var output1 = new OutputFile { Filename = "img.png", Subfolder = "a", Type = "output" };
            var output2 = new OutputFile { Filename = "img.png", Subfolder = "b", Type = "output" };

            var path1 = await service.GetOrFetchAsync(client, "pid-1", output1, _cacheDir);
            var path2 = await service.GetOrFetchAsync(client, "pid-1", output2, _cacheDir);

            Assert.NotEqual(path1, path2);
            Assert.Equal(2, client.GetImageCallCount);
        }
    }
}
