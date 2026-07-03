using System.IO;
using ComfyUILibs.Common;

namespace ComfyUILibsTests.Common
{
    public class SettingTests : IDisposable
    {
        private readonly string _tempDir;

        public SettingTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        private string TempPath(string name) => Path.Combine(_tempDir, name);

        private class Config
        {
            public string Host { get; set; } = "localhost";
            public int Port { get; set; } = 8080;
        }

        // ── コンストラクター ───────────────────────────────────────────────────

        [Fact]
        public void Constructor_ExistingFile_LoadsDataOnInit()
        {
            var path = TempPath("workflow_config.json");
            JsonLoader.WriteJson(path, new Config { Host = "comfyui", Port = 8188 });

            var setting = new Setting<Config>(path);

            Assert.Equal("comfyui", setting.Data.Host);
            Assert.Equal(8188, setting.Data.Port);
        }

        [Fact]
        public void Constructor_FileNotExists_CreatesFileWithDefaultData()
        {
            var path = TempPath("new_config.json");

            var setting = new Setting<Config>(path);

            Assert.True(File.Exists(path));
            Assert.Equal("localhost", setting.Data.Host);
            Assert.Equal(8080, setting.Data.Port);
        }

        [Fact]
        public void Constructor_OnLoadFalse_DoesNotCreateOrReadFile()
        {
            var path = TempPath("no_load.json");

            var setting = new Setting<Config>(path, onLoad: false);

            Assert.False(File.Exists(path));
            Assert.Equal("localhost", setting.Data.Host);
            Assert.Equal(8080, setting.Data.Port);
        }

        // ── Load ──────────────────────────────────────────────────────────────

        [Fact]
        public void Load_ExistingFile_UpdatesData()
        {
            var path = TempPath("load_test.json");
            JsonLoader.WriteJson(path, new Config { Host = "remotehost", Port = 9000 });
            var setting = new Setting<Config>(path, onLoad: false);

            setting.Load();

            Assert.Equal("remotehost", setting.Data.Host);
            Assert.Equal(9000, setting.Data.Port);
        }

        [Fact]
        public void Load_FileNotExists_CreatesFileAndSetsDefault()
        {
            var path = TempPath("auto_create.json");
            var setting = new Setting<Config>(path, onLoad: false);

            setting.Load();

            Assert.True(File.Exists(path));
            Assert.Equal("localhost", setting.Data.Host);
            Assert.Equal(8080, setting.Data.Port);
        }

        [Fact]
        public void Load_CalledTwice_ReloadsFromFile()
        {
            var path = TempPath("reload.json");
            JsonLoader.WriteJson(path, new Config { Host = "first", Port = 1 });
            var setting = new Setting<Config>(path);

            JsonLoader.WriteJson(path, new Config { Host = "second", Port = 2 });
            setting.Load();

            Assert.Equal("second", setting.Data.Host);
            Assert.Equal(2, setting.Data.Port);
        }

        // ── Save ──────────────────────────────────────────────────────────────

        [Fact]
        public void Save_WritesCurrentDataToFile()
        {
            var path = TempPath("save_test.json");
            var setting = new Setting<Config>(path, onLoad: false);
            setting.Data.Host = "saved_host";
            setting.Data.Port = 1234;

            setting.Save();

            var loaded = JsonLoader.ReadJson<Config>(path);
            Assert.Equal("saved_host", loaded.Host);
            Assert.Equal(1234, loaded.Port);
        }

        [Fact]
        public void Save_OverwritesExistingFile()
        {
            var path = TempPath("overwrite.json");
            JsonLoader.WriteJson(path, new Config { Host = "old", Port = 1 });
            var setting = new Setting<Config>(path);
            setting.Data.Host = "new";
            setting.Data.Port = 2;

            setting.Save();

            var loaded = JsonLoader.ReadJson<Config>(path);
            Assert.Equal("new", loaded.Host);
            Assert.Equal(2, loaded.Port);
        }

        [Fact]
        public void Save_ThenLoad_RoundTrip()
        {
            var path = TempPath("roundtrip.json");
            var setting = new Setting<Config>(path, onLoad: false);
            setting.Data.Host = "roundtrip_host";
            setting.Data.Port = 5555;

            setting.Save();
            setting.Data = new Config();
            setting.Load();

            Assert.Equal("roundtrip_host", setting.Data.Host);
            Assert.Equal(5555, setting.Data.Port);
        }
    }
}
