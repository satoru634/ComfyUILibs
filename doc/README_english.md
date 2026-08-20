# ComfyUILibs

✨ [日本語](../README.md)

A .NET 8 class library providing business logic for ComfyUI workflow execution, WebSocket monitoring, and configuration management.  
This is a C# port of the Python implementation from [comfyui_tools](https://github.com/satoru634/comfyui_tools), designed to be shared between a WPF GUI application and a future Discord bot.

---

## Features

| Feature | Class |
|---|---|
| Orchestrates the full workflow execution pipeline | `WorkflowRunner` |
| Loads and validates workflow_config.json | `ConfigLoader` |
| Selects templates and applies prompts / LoRA / image size | `WorkflowBuilder` |
| ComfyUI REST API and WebSocket client | `ComfyUIClient` |
| Runs WD14 Tagger workflows (via ComfyUI) | `Wd14TaggerRunner` |
| Runs tagging via a local wdv3-timm persistent process (no ComfyUI required) | `WdV3TimmTaggerRunner` |
| Abstracts a single-image tagging runner | `ITaggerRunner` (implemented by `Wd14TaggerRunner`/`WdV3TimmTaggerRunner`) |
| Batch-tags directories, applies tag filters, and generates tag frequency reports | `CaptioningService` |
| Manages the local cache of generated image previews | `PreviewImageCacheService` |
| Persists settings to JSON files | `Setting<T>` |
| Localizes exception messages (Japanese / English) | `Resources.Messages` |

---

## Tech Stack

- .NET 8 (`net8.0-windows10.0.17763.0`)
- `System.Net.Http.HttpClient` — REST API calls
- `System.Net.WebSockets.ClientWebSocket` — WebSocket monitoring
- `System.Text.Json` — JSON serialization
- `CommunityToolkit.Mvvm 8.4.2` — `ObservableObject` base class

---

## Directory Structure

```
ComfyUILibs/
  Base/
    ObservablePoint.cs        # INotifyPropertyChanged-aware coordinate wrapper
    ObservableSize.cs         # INotifyPropertyChanged-aware size wrapper
  Common/
    JsonLoader.cs             # Static JSON file read/write utility
    Setting.cs                # Generic settings persistence class
  Exceptions/
    ComfyUIException.cs       # Base exception class
  Resources/
    Messages.resx             # Exception messages (default, Japanese)
    Messages.en.resx          # Exception messages (English satellite)
    Messages.cs               # Static helper that resolves messages based on CurrentUICulture
  Models/
    WorkflowConfig.cs         # workflow_config.json model
    WorkflowInput.cs          # Input JSON model
    WorkflowResult.cs         # Execution result model
    ResolvedLora.cs           # Resolved LoRA entry
    TagResult.cs              # WD14 Tagger execution result model
    CaptioningProgress.cs     # Progress notification model for CaptioningService (includes the CaptioningResult enum)
  Services/
    IComfyUIClient.cs         # ComfyUIClient interface (for DI / testing)
    ComfyUIClient.cs          # ComfyUI REST API + WebSocket client (includes image fetch via GET /view)
    ConfigLoader.cs           # workflow_config.json loading and validation
    WorkflowBuilder.cs        # Template selection and patching
    WorkflowRunner.cs         # Workflow execution facade
    ITaggerRunner.cs          # Abstracts a single-image tagging runner (implemented by Wd14TaggerRunner/WdV3TimmTaggerRunner)
    Wd14TaggerRunner.cs       # WD14 Tagger workflow execution (via ComfyUI)
    IWdV3TimmProcessClient.cs # Abstracts stdin/stdout communication with the wdv3-timm persistent process (for DI / testing)
    WdV3TimmProcessClient.cs  # Default implementation: launches wdv3_timm.exe with --serve and speaks JSON Lines
    WdV3TimmModelMap.cs       # Maps wd14_tagger.model_name to wdv3-timm's --model value
    WdV3TimmPaths.cs          # Fixed-path convention for wdv3_timm.exe (next to the consuming app's own executable)
    WdV3TimmTaggerRunner.cs   # Tagging via a local wdv3-timm persistent process (no ComfyUI required;
                               # model name and thresholds are shared with the wd14_tagger section)
    CaptioningService.cs      # Batch directory tagging, tag filters, tag frequency reports
    IPreviewImageCacheService.cs # Preview image cache interface (for DI / testing)
    PreviewImageCacheService.cs  # Local cache management for generated image previews
  doc/
    README_english.md         # This file
```

---

## workflow_config.json

The configuration file referenced by `WorkflowRunner` and `Wd14TaggerRunner`.

```json
{
  "comfyui_url": "http://127.0.0.1:8188",
  "default_workflow": "sdxl",
  "workflows": {
    "sdxl": {
      "default_image_size": { "width": 832, "height": 1216 },
      "image_size": {
        "vertical":   { "width": 832,  "height": 1216 },
        "horizontal": { "width": 1216, "height": 832  },
        "square":     { "width": 1024, "height": 1024 }
      },
      "loras": {
        "my_lora": { "file": "my_lora.safetensors", "strength": 0.8 }
      }
    }
  },
  "wd14_tagger": {
    "model_name": "wd-eva02-large-tagger-v3",
    "general_threshold": 0.35,
    "character_threshold": 0.85
  },
  "prepend_tags": ["my_chara"],
  "exclude_tags": ["rating:general"]
}
```

`prepend_tags`/`exclude_tags` are exposed via `ITaggerRunner` (`Wd14TaggerRunner`/`WdV3TimmTaggerRunner`)'s `PrependTags`/`ExcludeTags` properties (an empty list if the key itself is absent). They are not validated; the caller of `CaptioningService` (e.g. the GUI) is expected to resolve the union with any additional values before use.

`WdV3TimmTaggerRunner` does not carry its own model name or thresholds — it shares `wd14_tagger` (`model_name`/`general_threshold`/`character_threshold`) instead, so the two backends can't drift out of sync. This means using it requires a valid `wd14_tagger` section. `wd14_tagger.model_name` (the ComfyUI-side Hugging Face repo name, e.g. `wd-eva02-large-tagger-v3`) is translated to wdv3-timm's `--model` value (e.g. `eva02`) via `WdV3TimmModelMap`. The mapping:

| `wd14_tagger.model_name` | wdv3-timm `--model` |
|---|---|
| `wd-vit-tagger-v3` | `vit` |
| `wd-swinv2-tagger-v3` | `swinv2` |
| `wd-convnext-tagger-v3` | `convnext` |
| `wd-eva02-large-tagger-v3` | `eva02` |
| `wd-vit-large-tagger-v3` | `vit-large` |

### Validation Rules

| Field | Rule |
|---|---|
| `comfyui_url` | Required, non-empty (only when using `Wd14TaggerRunner`) |
| `default_workflow` | Must match a key in `workflows` |
| `image_size.{orientation}` | All three keys (`vertical`, `horizontal`, `square`) are required |
| `width` / `height` | Integer in 512–2048, multiple of 8 |
| `loras[*].file` | Non-empty string |
| `loras[*].strength` | Numeric value required (missing key is rejected) |
| `wd14_tagger.general_threshold` / `character_threshold` | 0.0–1.0 |
| `wd14_tagger` section (when using `WdV3TimmTaggerRunner`) | Same `wd14_tagger.*` rules above, plus `model_name` must appear in the mapping table above |

`wdv3_timm.exe`'s executable path is not configured in the config file — it always resolves to `WdV3TimmPaths.ExeFilePath` (the fixed path `wdv3-timm\wdv3_timm.exe` next to the consuming app's own executable).

---

## Usage

### Running a Workflow

```csharp
// WorkflowRunner — facade that orchestrates the full execution pipeline
var runner = new WorkflowRunner("workflow_config.json", "sdxl");

var loras = new List<string> { "my_lora" };
var prompts = new PromptPair { Positive = "1girl, solo", Negative = "bad quality" };
var imageSize = new ImageSize { Width = 832, Height = 1216 };

var outputs = await runner.ExecuteAsync(loras, prompts, imageSize);
// outputs: list of OutputFile generated by ComfyUI
// Right after completion is detected, ComfyUI's history may not be updated yet and
// GetOutputsAsync can return an empty list; if so, it is retried up to 3 times at 300ms intervals

// Passing filenamePrefix overrides the SaveImage node's filename_prefix.
// If null or whitespace-only, the value already written in the template is kept as-is.
var outputsWithPrefix = await runner.ExecuteAsync(loras, prompts, imageSize, filenamePrefix: "my_batch");

// Metadata available after execution
Console.WriteLine(runner.PromptId);     // prompt_id assigned by ComfyUI
Console.WriteLine(runner.TemplatePath); // path to the template that was used
```

### Running from an Input JSON File

```csharp
// Reads input.json and writes the result to result.json
var runner = new WorkflowRunner("workflow_config.json", "sdxl");
await runner.RunAsync("input.json", "result.json");
```

#### input.json format

```json
{
  "loras": ["my_lora"],
  "prompts": {
    "positive": "1girl, solo",
    "negative": "bad quality"
  },
  "image_size": { "width": 832, "height": 1216 }
}
```

### WD14 Tagger (via ComfyUI)

```csharp
var tagger = new Wd14TaggerRunner("workflow_config.json");
var imageData = File.ReadAllBytes("input.png");
var tags = await tagger.TagAsync(imageData);
// tags: "1girl, solo, smile, ..."
```

### wdv3-timm (via a local process, no ComfyUI required)

`WdV3TimmTaggerRunner` shares model name and thresholds with the `wd14_tagger` section instead of
carrying its own, so `workflow_config.json` needs a valid `wd14_tagger` section (`model_name` must
be one the `WdV3TimmModelMap` mapping table above can translate). The executable itself
(`wdv3_timm.exe`) is launched from `WdV3TimmPaths.ExeFilePath` — a fixed path next to the consuming
app's own executable — so it is not configured here.

```csharp
// WdV3TimmTaggerRunner — launches the local wdv3_timm.exe in a persistent server mode and tags
// images against it. Launching a fresh process per image would reload the model every time, which
// is prohibitively slow, so the process is started lazily on the first TagAsync call and reused
// for every subsequent call.
await using var tagger = new WdV3TimmTaggerRunner("workflow_config.json");
var imageData = File.ReadAllBytes("input.png");
var tags = await tagger.TagAsync(imageData, "input.png");
// tags: "1girl, solo, smile, ..."

// The process is still started only once even when tagging many images
foreach (var path in Directory.EnumerateFiles("./images", "*.png"))
    await tagger.TagAsync(File.ReadAllBytes(path), Path.GetFileName(path));

// Leaving the `await using` scope calls DisposeAsync, which terminates the persistent process
```

wdv3_timm.py keeps underscores in tag names as part of its protocol contract, so passing its
response straight through would produce underscore-separated tags like `blue_eyes`. To match the
look of tags returned by `Wd14TaggerRunner` (via ComfyUI, whose WD Timm Tagger custom node already
converts underscores to spaces), `WdV3TimmTaggerRunner` normalizes each tag's underscores to spaces
when it receives the response (e.g. `blue_eyes` -> `blue eyes`). Emoticon-style tags such as
`^_^`/`;_;`/`>_<` (3 characters or fewer) are left untouched, following the common convention among
WD14 Tagger tooling, since converting them would break their meaning.

> **Note**: Implementing the `--serve` persistent server mode on the wdv3-timm side
> (`wdv3_timm.exe` / `wdv3_timm.py`) is out of scope for this library (a separate task in the
> wdv3-timm repository). It must follow the protocol contract documented in
> `IWdV3TimmProcessClient`'s XML doc comments (launch arguments, the `{"status":"ready"}` signal,
> the one-line-JSON request/response format, and termination via stdin EOF).

### Batch Directory Tagging (CaptioningService)

`CaptioningService` does not load its own configuration file; the caller passes in an
`ITaggerRunner` (either `Wd14TaggerRunner` or `WdV3TimmTaggerRunner`) plus the prepend/exclude
tags (with the union of config-file values and any extra values already resolved by the caller).

```csharp
var tagger = new Wd14TaggerRunner("workflow_config.json");
var service = new CaptioningService(
    tagger,
    prependTags: new List<string> { "my_chara" },
    excludeTags: new List<string> { "rating:general" });

var progress = new Progress<CaptioningProgress>(p =>
    Console.WriteLine($"[{p.Current}/{p.Total}] {p.FileName} -> {p.Result}"));

var (processed, skipped, errors) = await service.ProcessDirectoryAsync(
    "./images", recursive: true, overwrite: false, progress);
Console.WriteLine($"Done: processed {processed}, skipped {skipped}, errors {errors}");

// Aggregates every .txt file in the directory into tags_report.txt (tags_report.txt itself is excluded)
await service.GenerateReportAsync("./images", recursive: true);
```

- Tag filtering order: `(1) remove excluded tags -> (2) remove tags duplicated with prepend tags -> (3) insert prepend tags at the front` (exact match, case-insensitive)
- Supported extensions: `.jpg` `.jpeg` `.png` `.webp`
- If an exception occurs while processing a single image, the batch continues and `CaptioningProgress.Result` is reported as `Error` (the only case where `ProcessDirectoryAsync` itself throws is when the target directory does not exist)

### Fetching a Cached Preview Image

```csharp
// PreviewImageCacheService — fetches an image via GET /view and caches it locally
var cacheService = new PreviewImageCacheService();
var client = new ComfyUIClient("http://127.0.0.1:8188");

// Returns the cached file if present; otherwise fetches from ComfyUI and caches it.
// Returns null (never throws) if the file isn't an image or the fetch fails.
string? cachedPath = await cacheService.GetOrFetchAsync(
    client, promptId: "abc-123", output: outputFile, cacheDirectory: "preview_cache");
```

### Settings Persistence

```csharp
// If the settings file does not exist, it is created with default values
var setting = new Setting<MyConfig>("app_setting.json");
setting.Data.SomeValue = "changed";
setting.Save();
```

---

## Localization (exception messages)

Messages thrown by `ComfyUIException` are managed in `Resources/Messages.resx` (default, Japanese) and `Messages.en.resx` (English), and are automatically resolved based on `CultureInfo.CurrentUICulture`.

```csharp
using System.Globalization;
using ComfyUILibs.Resources;

// If the caller (e.g. the WPF GUI) switches CurrentUICulture, subsequently thrown
// ComfyUIException messages automatically switch to that language too
CultureInfo.CurrentUICulture = new CultureInfo("en");

try
{
    ConfigLoader.LoadConfig("workflow_config.json");
}
catch (ComfyUIException ex)
{
    Console.WriteLine(ex.Message); // English message
}
```

- The default (neutral resource) is Japanese. English cultures such as `en` / `en-US` use `Messages.en.resx`.
- To pin a specific default language regardless of the OS locale, explicitly set `CultureInfo.CurrentUICulture` at application startup.
- When adding a new message, add the key to both `Messages.resx` (Japanese) and `Messages.en.resx` (English), and reference it via `Resources.Messages.Get("Key")` / `Get("Key", args...)`.

---

## Template Files

`WorkflowRunner` looks for templates in the `templates/` directory next to the executable.

```
templates/
  {workflow_name}/
    template_lora_0.json   # 0 LoRAs
    template_lora_1.json   # 1 LoRA
    template_lora_2.json   # 2 LoRAs
    template_lora_3.json   # 3 LoRAs
    template_lora_4.json   # 4 LoRAs
  template_wd14_tagger.json
```

---

## Tests

Unit tests using xUnit v3 are located in `ComfyUILibsTests/`.

```
dotnet test ComfyUILibs.sln
```

| Test file | Count | Description |
|---|---|---|
| `Base/ObservablePointTests.cs` | 10 | Coordinate conversion and property change notification |
| `Base/ObservableSizeTests.cs` | 10 | Size conversion and property change notification |
| `Ui/UIItemBaseModelTests.cs` | 17 | Item list management (Init/Add/Clear) and selection index |
| `Common/JsonLoaderTests.cs` | 13 | JSON read/write and error handling |
| `Common/SettingTests.cs` | 9 | Settings persistence and loading |
| `Exceptions/ComfyUIExceptionTests.cs` | 3 | ComfyUIException construction and inheritance |
| `Services/ConfigLoaderTests.cs` | 48 | Validation — happy path and error cases (WdV3TimmTaggerRunner validation now only checks model-name mapping; the wdv3_timm section itself was removed) |
| `Services/ComfyUIClientTests.cs` | 13 | Mocked with FakeHttpMessageHandler (includes GetImageAsync) |
| `Services/WorkflowBuilderTests.cs` | 20 | Template selection and patching (includes filename_prefix override) |
| `Services/WorkflowRunnerTests.cs` | 13 | Mocked with FakeComfyUIClient (includes empty-outputs retry and filenamePrefix propagation) |
| `Services/Wd14TaggerRunnerTests.cs` | 11 | Tag extraction flow, PrependTags/ExcludeTags, output retry |
| `Services/WdV3TimmTaggerRunnerTests.cs` | 19 | Mocked with FakeWdV3TimmProcessClient (config validation, lazy process startup, launch arguments using the fixed WdV3TimmPaths.ExeFilePath, temp files, response handling, underscore-to-space tag normalization preserving emoticon tags, DisposeAsync) |
| `Services/WdV3TimmModelMapTests.cs` | 9 | wd14_tagger.model_name ⇔ wdv3-timm --model mapping, listing supported names, case-insensitivity, unknown model names |
| `Services/CaptioningServiceTests.cs` | 14 | Tag filtering, batch directory processing (recursive/overwrite/error continuation/progress), tag frequency reports, combined with a direct `ITaggerRunner` implementation |
| `Services/PreviewImageCacheServiceTests.cs` | 11 | Image detection, cache hit/miss, failure handling |
| `Models/TagResultTests.cs` | 3 | Default values, serialization/deserialization |
| `Resources/MessagesTests.cs` | 6 | Message resolution for ja/en/en-US, formatting, unknown-key behavior |

Total: **229 tests**

---

## License

See [LICENSE](../LICENSE).
