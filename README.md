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
| WD14 Tagger ワークフロー実行（ComfyUI 経由） | `Wd14TaggerRunner` |
| wdv3-timm 常駐プロセス経由のタグ付け実行（ComfyUI 不要） | `WdV3TimmTaggerRunner` |
| 画像 1 枚のタグ付けランナー抽象化 | `ITaggerRunner`（`Wd14TaggerRunner`/`WdV3TimmTaggerRunner` が実装） |
| ディレクトリ一括タグ付け・タグフィルタ・タグ集計レポート | `CaptioningService` |
| 生成画像プレビューのローカルキャッシュ管理 | `PreviewImageCacheService` |
| 設定ファイル永続化 | `Setting<T>` |
| 例外メッセージの多言語化（日本語/英語） | `Resources.Messages` |

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
  Resources/
    Messages.resx             # 例外メッセージ（既定・日本語）
    Messages.en.resx          # 例外メッセージ（英語サテライト）
    Messages.cs               # CurrentUICulture に応じてメッセージを解決する静的ヘルパー
  Models/
    WorkflowConfig.cs         # workflow_config.json モデル
    WorkflowInput.cs          # 入力 JSON モデル
    WorkflowResult.cs         # 実行結果モデル
    ResolvedLora.cs           # LoRA 解決済みエントリ
    TagResult.cs              # WD14 Tagger 実行結果モデル
    CaptioningProgress.cs     # CaptioningService の進捗通知モデル（CaptioningResult 列挙体を含む）
  Services/
    IComfyUIClient.cs         # ComfyUIClient インターフェース（DI / テスト用）
    ComfyUIClient.cs          # ComfyUI REST API + WebSocket クライアント（GET /view による画像取得を含む）
    ConfigLoader.cs           # workflow_config.json 読み込み・バリデーション
    WorkflowBuilder.cs        # テンプレート選択・書き換え
    WorkflowRunner.cs         # ワークフロー実行ファサード
    ITaggerRunner.cs          # 画像 1 枚のタグ付けランナー抽象化（Wd14TaggerRunner/WdV3TimmTaggerRunner が実装）
    Wd14TaggerRunner.cs       # WD14 Tagger ワークフロー実行（ComfyUI 経由）
    IWdV3TimmProcessClient.cs # wdv3-timm 常駐サーバープロセスとの標準入出力通信の抽象化（DI / テスト用）
    WdV3TimmProcessClient.cs  # wdv3_timm.exe を --serve で常駐起動し JSON Lines で通信する既定実装
    WdV3TimmTaggerRunner.cs   # wdv3-timm 常駐プロセス経由のタグ付け実行（ComfyUI 不要）
    CaptioningService.cs      # ディレクトリ一括タグ付け・タグフィルタ・タグ集計レポート
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
  },
  "wdv3_timm": {
    "exe_path": "E:\\Python_project\\wdv3-timm\\wdv3_timm.exe",
    "model": "vit",
    "general_threshold": 0.35,
    "character_threshold": 0.75
  },
  "prepend_tags": ["my_chara"],
  "exclude_tags": ["rating:general"]
}
```

`prepend_tags`/`exclude_tags` は `ITaggerRunner`（`Wd14TaggerRunner`/`WdV3TimmTaggerRunner`）の `PrependTags`/`ExcludeTags` プロパティ経由で参照できる（キー自体が存在しない場合は空リスト）。バリデーション対象ではなく、`CaptioningService` を呼び出す側（GUI 等）が追加指定値との union を解決してから利用する想定。

`wd14_tagger`（ComfyUI 経由）と `wdv3_timm`（ローカルプロセス経由）は排他ではなく、使用するバックエンドに応じて必要な方のセクションのみを用意すればよい（`Wd14TaggerRunner`/`WdV3TimmTaggerRunner` はそれぞれ自分のセクションのみを検証する）。

### バリデーションルール

| フィールド | ルール |
|---|---|
| `comfyui_url` | 必須・空文字不可（`Wd14TaggerRunner` 利用時のみ） |
| `default_workflow` | `workflows` のキーと一致すること |
| `image_size.{向き}` | `vertical` / `horizontal` / `square` の 3 キーが必須 |
| `width` / `height` | 512〜2048 の整数、8 の倍数 |
| `loras[*].file` | 空文字不可 |
| `loras[*].strength` | 数値必須（キー欠落不可） |
| `wd14_tagger.general_threshold` / `character_threshold` | 0.0〜1.0 |
| `wdv3_timm.exe_path` | 必須・空文字不可 |
| `wdv3_timm.model` | `vit` / `swinv2` / `convnext` / `eva02` / `vit-large` のいずれか |
| `wdv3_timm.general_threshold` / `character_threshold` | 0.0〜1.0 |

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

// filenamePrefix を指定すると SaveImage ノードの filename_prefix を上書きできる。
// null または空白のみの場合はテンプレートに記述された値をそのまま使用する。
var outputsWithPrefix = await runner.ExecuteAsync(loras, prompts, imageSize, filenamePrefix: "my_batch");

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

### WD14 Tagger（ComfyUI 経由）

```csharp
var tagger = new Wd14TaggerRunner("workflow_config.json");
var imageData = File.ReadAllBytes("input.png");
var tags = await tagger.TagAsync(imageData);
// tags: "1girl, solo, smile, ..."
```

### wdv3-timm（ローカルプロセス経由、ComfyUI 不要）

```csharp
// WdV3TimmTaggerRunner — ローカルの wdv3_timm.exe を常駐サーバーモードで起動してタグ付けする。
// 画像 1 枚ごとにプロセスを起動するとモデル再ロードのオーバーヘッドが大きいため、
// 初回 TagAsync 呼び出し時にプロセスを起動し、以降の呼び出しは同じプロセスを使い回す。
await using var tagger = new WdV3TimmTaggerRunner("workflow_config.json");
var imageData = File.ReadAllBytes("input.png");
var tags = await tagger.TagAsync(imageData, "input.png");
// tags: "1girl, solo, smile, ..."

// 複数画像をまとめて処理する場合もプロセスは 1 回だけ起動される
foreach (var path in Directory.EnumerateFiles("./images", "*.png"))
    await tagger.TagAsync(File.ReadAllBytes(path), Path.GetFileName(path));

// await using のスコープを抜けると DisposeAsync が常駐プロセスを終了する
```

> **注意**: wdv3-timm 側（`wdv3_timm.exe` / `wdv3_timm.py`）の `--serve` 常駐サーバーモードの実装は
> 本ライブラリの対象外（wdv3-timm リポジトリ側の別タスク）。
> `IWdV3TimmProcessClient` の XML ドキュメントコメントに記載のプロトコル契約（起動引数・
> `{"status":"ready"}` シグナル・1 行 1 JSON のリクエスト/応答形式・標準入力 EOF による終了）に
> 従って実装する必要がある。

### ディレクトリ一括タグ付け（CaptioningService）

`CaptioningService` は自前で設定ファイルを読み込まず、呼び出し側が `ITaggerRunner`
（`Wd14TaggerRunner` または `WdV3TimmTaggerRunner`）と
prepend/exclude タグ（設定ファイルと追加指定の union は呼び出し側で解決済みのもの）を渡す。

```csharp
var tagger = new Wd14TaggerRunner("workflow_config.json");
var service = new CaptioningService(
    tagger,
    prependTags: new List<string> { "my_chara" },
    excludeTags: new List<string> { "rating:general" });

var progress = new Progress<CaptioningProgress>(p =>
    Console.WriteLine($"[{p.Current}/{p.Total}] {p.FileName} → {p.Result}"));

var (processed, skipped, errors) = await service.ProcessDirectoryAsync(
    "./images", recursive: true, overwrite: false, progress);
Console.WriteLine($"完了: 処理 {processed}, スキップ {skipped}, エラー {errors}");

// ディレクトリ内の全 .txt を集計して tags_report.txt を出力（tags_report.txt 自身は集計対象外）
await service.GenerateReportAsync("./images", recursive: true);
```

- タグフィルタは `(1) exclude 除去 → (2) prepend と重複するタグの除去 → (3) prepend 先頭挿入` の順（完全一致・大文字小文字無視）
- 対応拡張子: `.jpg` `.jpeg` `.png` `.webp`
- 画像 1 枚の処理中に例外が発生した場合もバッチ処理は継続し、`CaptioningProgress.Result` が `Error` として通知される（`ProcessDirectoryAsync` 自体が例外で止まるのは、指定ディレクトリが存在しない場合のみ）

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

## 多言語化（例外メッセージ）

`ComfyUIException` がスローするメッセージは `Resources/Messages.resx`（既定・日本語）と `Messages.en.resx`（英語）で管理されており、`CultureInfo.CurrentUICulture` に応じて自動的に切り替わります。

```csharp
using System.Globalization;
using ComfyUILibs.Resources;

// 呼び出し側（WPF GUI 等）が CurrentUICulture を切り替えると、以降にスローされる
// ComfyUIException のメッセージも自動的にその言語になる
CultureInfo.CurrentUICulture = new CultureInfo("en");

try
{
    ConfigLoader.LoadConfig("workflow_config.json");
}
catch (ComfyUIException ex)
{
    Console.WriteLine(ex.Message); // 英語のメッセージ
}
```

- 既定（neutral resource）は日本語。`en`／`en-US` 等の英語カルチャでは `Messages.en.resx` が使用される
- OS ロケールに関わらず特定の言語を既定にしたい場合は、アプリ起動時に明示的に `CultureInfo.CurrentUICulture` をセットすること
- 新しいメッセージを追加する場合は `Messages.resx`（日本語）と `Messages.en.resx`（英語）の両方にキーを追加し、`Resources.Messages.Get("キー")` / `Get("キー", 引数...)` から参照する

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
| `Services/ConfigLoaderTests.cs` | 52 | 正常系・異常系のバリデーション（wdv3_timm セクションを含む） |
| `Services/ComfyUIClientTests.cs` | 13 | FakeHttpMessageHandler によるモック（GetImageAsync 含む） |
| `Services/WorkflowBuilderTests.cs` | 18 | テンプレート選択・適用（filename_prefix 上書きを含む） |
| `Services/WorkflowRunnerTests.cs` | 13 | FakeComfyUIClient によるモック（outputs 空リトライ・filenamePrefix 伝播を含む） |
| `Services/Wd14TaggerRunnerTests.cs` | 11 | タグ取得フロー・PrependTags/ExcludeTags・タグ取得リトライ |
| `Services/WdV3TimmTaggerRunnerTests.cs` | 16 | FakeWdV3TimmProcessClient によるモック（設定バリデーション・遅延プロセス起動・一時ファイル・応答解釈・DisposeAsync） |
| `Services/CaptioningServiceTests.cs` | 14 | タグフィルタ・ディレクトリ一括処理（再帰/上書き/エラー継続/進捗通知）・タグ集計レポート・ITaggerRunner 抽象の直接実装との組み合わせ |
| `Services/PreviewImageCacheServiceTests.cs` | 12 | 画像判定・キャッシュヒット/新規取得/失敗時の挙動 |
| `Models/TagResultTests.cs` | 3 | デフォルト値・シリアライズ/デシリアライズ |
| `Resources/MessagesTests.cs` | 6 | ja/en/en-US でのメッセージ解決・書式指定・未知キーの挙動 |

合計: **218 件**

---

## ライセンス

[LICENSE](LICENSE) を参照してください。
