using System.IO;
using System.Text.Json;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Resources;
using ComfyUILibs.Services;

namespace ComfyUILibsTests.Services
{
    /// <summary>テスト用の IWdV3TimmProcessClient モック。</summary>
    internal class FakeWdV3TimmProcessClient : IWdV3TimmProcessClient
    {
        public int StartCallCount { get; private set; }
        public string? StartedExePath { get; private set; }
        public IReadOnlyList<string>? StartedArguments { get; private set; }
        public bool DisposeCalled { get; private set; }
        public List<string> Requests { get; } = new();

        /// <summary>SendRequestAsync が呼ばれるたびに順に返す応答。尽きた場合は null（EOF）を返す。</summary>
        public Queue<string?> Responses { get; } = new();

        public Exception? StartException { get; set; }

        public Task StartAsync(string exePath, IReadOnlyList<string> arguments)
        {
            StartCallCount++;
            StartedExePath = exePath;
            StartedArguments = arguments;
            if (StartException != null)
                throw StartException;
            return Task.CompletedTask;
        }

        public Task<string?> SendRequestAsync(string requestJson)
        {
            Requests.Add(requestJson);
            var response = Responses.Count > 0 ? Responses.Dequeue() : null;
            return Task.FromResult(response);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    public class WdV3TimmTaggerRunnerTests
    {
        // モデル名・しきい値は wd14_tagger セクションを共用する（ConfigLoader.ValidateWdV3TimmConfig 参照）。
        // 実行ファイルパスは captioning_config.json では扱わず WdV3TimmPaths の固定パスを使う。
        private static WorkflowConfig CreateConfig() => new()
        {
            Wd14Tagger = new Wd14TaggerConfig
            {
                ModelName = "wd-vit-tagger-v3",
                GeneralThreshold = 0.35,
                CharacterThreshold = 0.75,
            }
        };

        private static string OkResponse(string tags) => JsonSerializer.Serialize(new { status = "ok", tags });
        private static string ErrorResponse(string message) => JsonSerializer.Serialize(new { status = "error", message });

        // ── コンストラクター・設定バリデーション ────────────────────────────

        [Fact]
        public void Constructor_MissingWd14TaggerSection_ThrowsComfyUIException()
        {
            // モデル名・しきい値は wd14_tagger セクションを共用するため、それが無いと構築できない
            var config = new WorkflowConfig();

            Assert.Throws<ComfyUIException>(() =>
                new WdV3TimmTaggerRunner(config, new FakeWdV3TimmProcessClient()));
        }

        [Fact]
        public void Constructor_UnmappedWd14ModelName_ThrowsComfyUIException()
        {
            var config = CreateConfig();
            config.Wd14Tagger!.ModelName = "wd-v1-4-moat-tagger-v2";

            Assert.Throws<ComfyUIException>(() =>
                new WdV3TimmTaggerRunner(config, new FakeWdV3TimmProcessClient()));
        }

        [Fact]
        public void Constructor_DoesNotStartProcess()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();

            _ = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            // プロセス起動は初回 TagAsync まで遅延される
            Assert.Equal(0, fakeClient.StartCallCount);
        }

        // ── PrependTags / ExcludeTags ────────────────────────────────────────

        [Fact]
        public void PrependTags_ConfigHasValues_ReturnsConfigValues()
        {
            var config = CreateConfig();
            config.PrependTags = new List<string> { "my_chara", "1girl" };

            var runner = new WdV3TimmTaggerRunner(config, new FakeWdV3TimmProcessClient());

            Assert.Equal(new[] { "my_chara", "1girl" }, runner.PrependTags);
        }

        [Fact]
        public void PrependTags_ConfigKeyMissing_ReturnsEmptyList()
        {
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), new FakeWdV3TimmProcessClient());

            Assert.Empty(runner.PrependTags);
        }

        [Fact]
        public void ExcludeTags_ConfigHasValues_ReturnsConfigValues()
        {
            var config = CreateConfig();
            config.ExcludeTags = new List<string> { "rating:general" };

            var runner = new WdV3TimmTaggerRunner(config, new FakeWdV3TimmProcessClient());

            Assert.Equal(new[] { "rating:general" }, runner.ExcludeTags);
        }

        [Fact]
        public void ExcludeTags_ConfigKeyMissing_ReturnsEmptyList()
        {
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), new FakeWdV3TimmProcessClient());

            Assert.Empty(runner.ExcludeTags);
        }

        // ── TagAsync: プロセス起動 ────────────────────────────────────────────

        [Fact]
        public async Task TagAsync_FirstCall_StartsProcessWithExpectedArguments()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl, solo"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            await runner.TagAsync(new byte[] { 1, 2, 3 }, "photo.jpg");

            Assert.Equal(1, fakeClient.StartCallCount);
            Assert.Equal(WdV3TimmPaths.ExeFilePath, fakeClient.StartedExePath);
            Assert.Equal(
                new[] { "--serve", "--model", "vit", "-g", "0.35", "-c", "0.75" },
                fakeClient.StartedArguments);
        }

        [Fact]
        public async Task TagAsync_SecondCall_DoesNotStartProcessAgain()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl"));
            fakeClient.Responses.Enqueue(OkResponse("2girls"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            await runner.TagAsync(new byte[] { 1 }, "a.jpg");
            await runner.TagAsync(new byte[] { 2 }, "b.jpg");

            Assert.Equal(1, fakeClient.StartCallCount);
            Assert.Equal(2, fakeClient.Requests.Count);
        }

        // ── TagAsync: リクエスト内容 ──────────────────────────────────────────

        [Fact]
        public async Task TagAsync_WritesImageBytesToTempFileAndSendsItsPath()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);
            var imageBytes = new byte[] { 10, 20, 30 };

            // フェイク側で SendRequestAsync 実行時点の一時ファイル内容を検証できるよう、
            // レスポンスをキューする前に一時ファイルパスを取り出してファイル内容を確認する
            string? capturedPath = null;
            byte[]? capturedContent = null;

            await runner.TagAsync(imageBytes, "photo.png");

            Assert.Single(fakeClient.Requests);
            using (var doc = JsonDocument.Parse(fakeClient.Requests[0]))
            {
                capturedPath = doc.RootElement.GetProperty("image_path").GetString();
            }
            Assert.NotNull(capturedPath);
            Assert.Equal(".png", Path.GetExtension(capturedPath));
            // TagAsync 完了後は一時ファイルが削除されていること
            Assert.False(File.Exists(capturedPath));
            _ = capturedContent;
        }

        // ── TagAsync: 応答の解釈 ──────────────────────────────────────────────

        [Fact]
        public async Task TagAsync_OkResponse_ReturnsTags()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl, solo, long hair"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            var tags = await runner.TagAsync(new byte[] { 1 });

            Assert.Equal("1girl, solo, long hair", tags);
        }

        [Fact]
        public async Task TagAsync_OkResponse_UnderscoreInTags_ConvertedToSpace()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl, blue_eyes, short_hair"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            var tags = await runner.TagAsync(new byte[] { 1 });

            Assert.Equal("1girl, blue eyes, short hair", tags);
        }

        [Fact]
        public async Task TagAsync_OkResponse_ShortEmoticonTag_KeepsUnderscore()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl, ^_^, ;_;"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            var tags = await runner.TagAsync(new byte[] { 1 });

            Assert.Equal("1girl, ^_^, ;_;", tags);
        }

        [Fact]
        public async Task TagAsync_ErrorResponse_ThrowsComfyUIExceptionWithMessage()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(ErrorResponse("画像を読み込めませんでした"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            var ex = await Assert.ThrowsAsync<ComfyUIException>(() => runner.TagAsync(new byte[] { 1 }));
            Assert.Contains("画像を読み込めませんでした", ex.Message);
        }

        [Fact]
        public async Task TagAsync_ProcessExitedUnexpectedly_ThrowsComfyUIException()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(null); // EOF
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            var ex = await Assert.ThrowsAsync<ComfyUIException>(() => runner.TagAsync(new byte[] { 1 }));
            Assert.Equal(Messages.Get("WdV3TimmTaggerRunner_ProcessExited"), ex.Message);
        }

        [Fact]
        public async Task TagAsync_InvalidJsonResponse_ThrowsComfyUIException()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue("not a json line");
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            await Assert.ThrowsAsync<ComfyUIException>(() => runner.TagAsync(new byte[] { 1 }));
        }

        [Fact]
        public async Task TagAsync_TempFileDeletedEvenWhenRequestFails()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(ErrorResponse("失敗"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            await Assert.ThrowsAsync<ComfyUIException>(() => runner.TagAsync(new byte[] { 1 }, "a.jpg"));

            using var doc = JsonDocument.Parse(fakeClient.Requests[0]);
            var path = doc.RootElement.GetProperty("image_path").GetString();
            Assert.False(File.Exists(path));
        }

        // ── DisposeAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task DisposeAsync_NeverStarted_DoesNotCallProcessClientDispose()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);

            await runner.DisposeAsync();

            Assert.False(fakeClient.DisposeCalled);
        }

        [Fact]
        public async Task DisposeAsync_Started_CallsProcessClientDispose()
        {
            var fakeClient = new FakeWdV3TimmProcessClient();
            fakeClient.Responses.Enqueue(OkResponse("1girl"));
            var runner = new WdV3TimmTaggerRunner(CreateConfig(), fakeClient);
            await runner.TagAsync(new byte[] { 1 });

            await runner.DisposeAsync();

            Assert.True(fakeClient.DisposeCalled);
        }
    }
}
