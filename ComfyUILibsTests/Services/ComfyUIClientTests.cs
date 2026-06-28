using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    /// <summary>HTTP レスポンスを差し替えるテスト用 HttpMessageHandler</summary>
    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    internal static class FakeHttpClientFactory
    {
        public static HttpClient Create(Func<HttpRequestMessage, HttpResponseMessage> handler)
            => new HttpClient(new FakeHttpMessageHandler(handler));

        public static HttpClient CreateJson(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
            => Create(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
    }

    public class ComfyUIClientTests
    {
        private const string BaseUrl = "http://localhost:8188";

        // ── SubmitAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task SubmitAsync_ValidResponse_ReturnsPromptId()
        {
            var http = FakeHttpClientFactory.CreateJson("""{"prompt_id":"abc-123"}""");
            var client = new ComfyUIClient(BaseUrl, http);

            var promptId = await client.SubmitAsync(new JsonObject(), "client-1");

            Assert.Equal("abc-123", promptId);
        }

        [Fact]
        public async Task SubmitAsync_HttpError_ThrowsComfyUIException()
        {
            var http = FakeHttpClientFactory.CreateJson("{}", HttpStatusCode.InternalServerError);
            var client = new ComfyUIClient(BaseUrl, http);

            await Assert.ThrowsAsync<ComfyUIException>(() =>
                client.SubmitAsync(new JsonObject(), "client-1"));
        }

        [Fact]
        public async Task SubmitAsync_ConnectionRefused_ThrowsComfyUIException()
        {
            var http = new HttpClient(new FakeHttpMessageHandler(_ =>
                throw new HttpRequestException("Connection refused")));
            var client = new ComfyUIClient(BaseUrl, http);

            await Assert.ThrowsAsync<ComfyUIException>(() =>
                client.SubmitAsync(new JsonObject(), "client-1"));
        }

        // ── UploadImageAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task UploadImageAsync_ValidResponse_ReturnsName()
        {
            var http = FakeHttpClientFactory.CreateJson("""{"name":"uploaded.png"}""");
            var client = new ComfyUIClient(BaseUrl, http);

            var name = await client.UploadImageAsync(new byte[] { 1, 2, 3 }, "test.png");

            Assert.Equal("uploaded.png", name);
        }

        [Fact]
        public async Task UploadImageAsync_HttpError_ThrowsComfyUIException()
        {
            var http = FakeHttpClientFactory.CreateJson("{}", HttpStatusCode.BadRequest);
            var client = new ComfyUIClient(BaseUrl, http);

            await Assert.ThrowsAsync<ComfyUIException>(() =>
                client.UploadImageAsync(new byte[] { 0 }));
        }

        // ── GetHistoryAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetHistoryAsync_PromptIdExists_ReturnsEntry()
        {
            var json = """{"abc-123":{"outputs":{},"status":{}}}""";
            var http = FakeHttpClientFactory.CreateJson(json);
            var client = new ComfyUIClient(BaseUrl, http);

            var history = await client.GetHistoryAsync("abc-123");

            Assert.Equal(System.Text.Json.JsonValueKind.Object, history.ValueKind);
            Assert.True(history.TryGetProperty("outputs", out _));
        }

        [Fact]
        public async Task GetHistoryAsync_PromptIdNotFound_ReturnsEmptyObject()
        {
            var http = FakeHttpClientFactory.CreateJson("{}");
            var client = new ComfyUIClient(BaseUrl, http);

            var history = await client.GetHistoryAsync("missing-id");

            Assert.Equal(System.Text.Json.JsonValueKind.Object, history.ValueKind);
        }

        // ── GetOutputsAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetOutputsAsync_WithImages_ReturnsOutputFiles()
        {
            var json = """
                {
                  "abc-123": {
                    "outputs": {
                      "9": {
                        "images": [
                          {"filename": "ComfyUI_00001_.png", "subfolder": "", "type": "output"}
                        ]
                      }
                    }
                  }
                }
                """;
            var http = FakeHttpClientFactory.CreateJson(json);
            var client = new ComfyUIClient(BaseUrl, http);

            var outputs = await client.GetOutputsAsync("abc-123");

            Assert.Single(outputs);
            Assert.Equal("ComfyUI_00001_.png", outputs[0].Filename);
            Assert.Equal("output", outputs[0].Type);
        }

        [Fact]
        public async Task GetOutputsAsync_NoPromptId_ReturnsEmptyList()
        {
            var http = FakeHttpClientFactory.CreateJson("{}");
            var client = new ComfyUIClient(BaseUrl, http);

            var outputs = await client.GetOutputsAsync("missing-id");

            Assert.Empty(outputs);
        }

        [Fact]
        public async Task GetOutputsAsync_MultipleNodes_ReturnsAllImages()
        {
            var json = """
                {
                  "p1": {
                    "outputs": {
                      "9": {"images": [{"filename": "img1.png", "subfolder": "", "type": "output"}]},
                      "10": {"images": [{"filename": "img2.png", "subfolder": "", "type": "output"}]}
                    }
                  }
                }
                """;
            var http = FakeHttpClientFactory.CreateJson(json);
            var client = new ComfyUIClient(BaseUrl, http);

            var outputs = await client.GetOutputsAsync("p1");

            Assert.Equal(2, outputs.Count);
        }
    }
}
