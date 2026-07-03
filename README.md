# ComfyUILibs

✨ [English](doc/README_english.md)

ComfyUI のワークフロー実行・WebSocket 監視・設定管理などのビジネスロジックを提供する .NET 8 クラスライブラリです。  
[comfyui_tools](https://github.com/satoru634/comfyui_tools) の Python 実装を C# に移植したもので、WPF GUI アプリや将来の Discord ボットと共用することを前提に設計されています。

---

## 主な機能

| 機能 | クラス |
|---|---|
| ワークフロー実行の全工程を統括 | `WorkflowRunner` |
| workflow_config.json の読み込み・バリデーション | `ConfigLoader` |
| テンプレート選択・プロンプト/LoRA/サイズ適用 | `WorkflowBuilder` |
| ComfyUI REST API / WebSocket クライアント | `ComfyUIClient` |
| WD14 Tagger ワークフロー実行 | `Wd14TaggerRunner` |
| 生成画像プレビューのローカルキャッシュ管理 | `PreviewImageCacheService` |
| 設定ファイル永続化 | `Setting<T>` |

---

## 技術スタック

- .NET 8 (`net8.0-windows10.0.17763.0`)
- `System.Net.Http.HttpClient` — REST API 呼び出し
- `System.Net.WebSockets.ClientWebSocket` — WebSocket 監視
- `System.Text.Json` — JSON 操作
- `CommunityToolkit.Mvvm 8.4.2` — `ObservableObject` 基底クラス

---

## ディレクトリ構成

```
ComfyUILibs/
  Base/
    ObservablePoint.cs        # INotifyPropertyChanged 対応の座標ラッパー
    ObservableSize.cs         # INotifyPropertyChanged 対応のサイズラッパー
  Common/
    JsonLoader.cs             # JSON ファイル読み書き静的ユーティリティ
    Setting.cs                # 設定ファイル永続化ジェネリッククラス
  Exceptions/
    ComfyUIException.cs       # 基底例外クラス
  Models/
    WorkflowConfig.cs         # workflow_config.json モデル
    WorkflowInput.cs          # 入力 JSON モデル
    WorkflowResult.cs         # 実行結果モデル
    ResolvedLora.cs           # LoRA 解決済みエントリ
  Services/
    IComfyUIClient.cs         # ComfyUIClient インターフェース（DI / テスト用）
    ComfyUIClient.cs          # ComfyUI REST API + WebSocket クライアント（GET /view による画像取得を含む）
    ConfigLoader.cs           # workflow_config.json 読み込み・バリデーション
    WorkflowBuilder.cs        # テンプレート選択・書き換え
    WorkflowRunner.cs         # ワークフロー実行ファサード
    Wd14TaggerRunner.cs       # WD14 Tagger ワークフロー実行
    IPreviewImageCacheService.cs # プレビュー画像キャッシュのインターフェース（DI / テスト用）
    PreviewImageCacheService.cs  # 生成画像プレビューのローカルキャッシュ管理
  doc/
    README_english.md         # 英語版 README
```

---

## workflow_config.json

`WorkflowRunner` や `Wd14TaggerRunner` が参照する設定ファイルです。

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
  }
}
```

### バリデーションルール

| フィールド | ルール |
|---|---|
| `comfyui_url` | 必須・空文字不可 |
| `default_workflow` | `workflows` のキーと一致すること |
| `image_size.{向き}` | `vertical` / `horizontal` / `square` の 3 キーが必須 |
| `width` / `height` | 512〜2048 の整数、8 の倍数 |
| `loras[*].file` | 空文字不可 |
| `loras[*].strength` | 数値必須（キー欠落不可） |
| `wd14_tagger.general_threshold` / `character_threshold` | 0.0〜1.0 |

---

## 使い方

### ワークフロー実行

```csharp
// WorkflowRunner — ワークフロー実行の全工程を統括するファサード
var runner = new WorkflowRunner("workflow_config.json", "sdxl");

var loras = new List<string> { "my_lora" };
var prompts = new PromptPair { Positive = "1girl, solo", Negative = "bad quality" };
var imageSize = new ImageSize { Width = 832, Height = 1216 };

var outputs = await runner.ExecuteAsync(loras, prompts, imageSize);
// outputs: ComfyUI が生成したファイルのリスト（OutputFile）
// 完了検知直後に ComfyUI 側の history 反映が間に合わず空リストが返ることがあるため、
// 空だった場合は 300ms 間隔で最大 3 回まで自動リトライする

// 実行後のメタ情報
Console.WriteLine(runner.PromptId);    // ComfyUI の prompt_id
Console.WriteLine(runner.TemplatePath); // 使用したテンプレートのパス
```

### 入力 JSON ファイルから実行

```csharp
// input.json を読み込み、結果を result.json に書き出す
var runner = new WorkflowRunner("workflow_config.json", "sdxl");
await runner.RunAsync("input.json", "result.json");
```

#### input.json の形式

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

### WD14 Tagger

```csharp
var tagger = new Wd14TaggerRunner("workflow_config.json");
var imageData = File.ReadAllBytes("input.png");
var tags = await tagger.TagAsync(imageData);
// tags: "1girl, solo, smile, ..."
```

### 生成画像プレビューのキャッシュ取得

```csharp
// PreviewImageCacheService — 画像を GET /view で取得し、ローカルにキャッシュする
var cacheService = new PreviewImageCacheService();
var client = new ComfyUIClient("http://127.0.0.1:8188");

// キャッシュ済みならファイル I/O のみ、未取得なら ComfyUI から取得してキャッシュに保存する
// 取得に失敗した場合・画像ファイルでない場合は null を返す（例外は送出しない）
string? cachedPath = await cacheService.GetOrFetchAsync(
    client, promptId: "abc-123", output: outputFile, cacheDirectory: "preview_cache");
```

### 設定ファイルの永続化

```csharp
// 設定ファイルが存在しない場合はデフォルト値で自動作成される
var setting = new Setting<MyConfig>("app_setting.json");
setting.Data.SomeValue = "changed";
setting.Save();
```

---

## テンプレートファイル

`WorkflowRunner` は実行ディレクトリの `templates/` を参照します。

```
templates/
  {workflow_name}/
    template_lora_0.json   # LoRA 0 個用
    template_lora_1.json   # LoRA 1 個用
    template_lora_2.json   # LoRA 2 個用
    template_lora_3.json   # LoRA 3 個用
    template_lora_4.json   # LoRA 4 個用
  template_wd14_tagger.json
```

---

## テスト

xUnit v3 によるユニットテストが `ComfyUILibsTests/` に用意されています。

```
dotnet test ComfyUILibs.sln
```

| テストファイル | 件数 | 概要 |
|---|---|---|
| `Base/ObservablePointTests.cs` | 10 | 座標変換・プロパティ変更通知 |
| `Base/ObservableSizeTests.cs` | 10 | サイズ変換・プロパティ変更通知 |
| `Common/JsonLoaderTests.cs` | 13 | JSON 読み書き・エラーハンドリング |
| `Common/SettingTests.cs` | 9 | 設定の永続化・読み込み |
| `Exceptions/ComfyUIExceptionTests.cs` | 3 | ComfyUIException の構築・継承 |
| `Services/ConfigLoaderTests.cs` | 38 | 正常系・異常系のバリデーション |
| `Services/ComfyUIClientTests.cs` | 13 | FakeHttpMessageHandler によるモック（GetImageAsync 含む） |
| `Services/WorkflowBuilderTests.cs` | 14 | テンプレート選択・適用 |
| `Services/WorkflowRunnerTests.cs` | 11 | FakeComfyUIClient によるモック（outputs 空リトライを含む） |
| `Services/Wd14TaggerRunnerTests.cs` | 5 | タグ取得フロー |
| `Services/PreviewImageCacheServiceTests.cs` | 12 | 画像判定・キャッシュヒット/新規取得/失敗時の挙動 |

合計: **153 件**

---

## ライセンス

[LICENSE](LICENSE) を参照してください。
