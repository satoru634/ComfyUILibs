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
| 画像埋め込みメタデータ（ComfyUI prompt/workflow・A1111 parameters）の読み取り | `ImageMetadataReader` |
| プロンプト文字列 → 正規化タグ列 の変換 | `PromptTagExtractor` |
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
    ComfyImageMetadata.cs     # 画像埋め込みメタデータの抽出結果モデル（ComfyMetadataParseStatus / ComfyLoraRef を含む）
  Services/
    IComfyUIClient.cs         # ComfyUIClient インターフェース（DI / テスト用）
    ComfyUIClient.cs          # ComfyUI REST API + WebSocket クライアント（GET /view による画像取得を含む）
    ConfigLoader.cs           # workflow_config.json 読み込み・バリデーション
    WorkflowBuilder.cs        # テンプレート選択・書き換え
    WorkflowRunner.cs         # ワークフロー実行ファサード
    IImageMetadataReader.cs   # PNG 埋め込みメタデータ読み取りの抽象（DI / テスト用）
    ImageMetadataReader.cs    # PNG チャンクを直接走査し ComfyUI prompt / A1111 parameters を解析
    IPromptTagExtractor.cs    # プロンプト文字列 → 正規化タグ列 変換の抽象（DI / テスト用）
    PromptTagExtractor.cs     # <lora:...> 除去・重み記法剥がし・BREAK 除去・重複排除でタグ化
    ITaggerRunner.cs          # 画像 1 枚のタグ付けランナー抽象化（Wd14TaggerRunner/WdV3TimmTaggerRunner が実装）
    Wd14TaggerRunner.cs       # WD14 Tagger ワークフロー実行（ComfyUI 経由）
    IWdV3TimmProcessClient.cs # wdv3-timm 常駐サーバープロセスとの標準入出力通信の抽象化（DI / テスト用）
    WdV3TimmProcessClient.cs  # wdv3_timm.exe を --serve で常駐起動し JSON Lines で通信する既定実装
    WdV3TimmModelMap.cs       # wd14_tagger.model_name ⇔ wdv3-timm --model の対応表
    WdV3TimmPaths.cs          # wdv3_timm.exe の固定パス規約（利用側アプリの実行ファイルと
                               # 同階層の wdv3-timm フォルダ）
    WdV3TimmTaggerRunner.cs   # wdv3-timm 常駐プロセス経由のタグ付け実行（ComfyUI 不要、
                               # モデル名・しきい値は wd14_tagger セクションを共用、
                               # 実行ファイルパスは WdV3TimmPaths の固定パス）
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
  "prepend_tags": ["my_chara"],
  "exclude_tags": ["rating:general"]
}
```

`prepend_tags`/`exclude_tags` は `ITaggerRunner`（`Wd14TaggerRunner`/`WdV3TimmTaggerRunner`）の `PrependTags`/`ExcludeTags` プロパティ経由で参照できる（キー自体が存在しない場合は空リスト）。バリデーション対象ではなく、`CaptioningService` を呼び出す側（GUI 等）が追加指定値との union を解決してから利用する想定。

`WdV3TimmTaggerRunner` はモデル名・しきい値を自分では持たず、`wd14_tagger`（`model_name`/`general_threshold`/`character_threshold`）を共用する（ComfyUI 版と wdv3-timm 版で別々に管理すると設定がずれるため）。そのため利用するには有効な `wd14_tagger` セクションが必要。`wd14_tagger.model_name`（ComfyUI 側の Hugging Face リポジトリ名、例: `wd-eva02-large-tagger-v3`）は `WdV3TimmModelMap` で wdv3-timm 側の `--model` 値（例: `eva02`）に変換される。対応表は次のとおり:

wdv3_timm.exe の実行ファイルパスは config ファイルでは指定しない。`WdV3TimmPaths.ExeFilePath`（利用側アプリの実行ファイルと同じ階層の `wdv3-timm\wdv3_timm.exe` 固定パス）を常に使用する。

| `wd14_tagger.model_name` | wdv3-timm `--model` |
|---|---|
| `wd-vit-tagger-v3` | `vit` |
| `wd-swinv2-tagger-v3` | `swinv2` |
| `wd-convnext-tagger-v3` | `convnext` |
| `wd-eva02-large-tagger-v3` | `eva02` |
| `wd-vit-large-tagger-v3` | `vit-large` |

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
| （`WdV3TimmTaggerRunner` 利用時）`wd14_tagger` セクション | 上記 `wd14_tagger.*` のルールに加え、`model_name` が上記対応表に存在すること |

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

// CancellationToken を渡すと ComfyUI への送信・完了監視・結果取得の各待機を中断できる。
// ComfyUI サーバーがダウンして完了イベントも切断も検出できないまま待ち続ける状況でも、
// トークンをキャンセルすれば即座に OperationCanceledException で抜けられる
// （接続失敗・タイムアウトの ComfyUIException には変換されない）。
using var cts = new CancellationTokenSource();
var outputsCancelable = await runner.ExecuteAsync(loras, prompts, imageSize, cancellationToken: cts.Token);

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

### 画像メタデータの読み取り・タグ抽出

```csharp
// ImageMetadataReader — PNG の tEXt/iTXt/zTXt チャンクを直接走査し、
// ComfyUI の prompt（API 形式）を優先解析する。prompt が無ければ A1111 形式の
// parameters チャンクをフォールバック解析する。
IImageMetadataReader reader = new ImageMetadataReader();
ComfyImageMetadata meta = reader.Read("output/ComfyUI_00123_.png");

Console.WriteLine(meta.ParseStatus);      // Ok / Partial / None
Console.WriteLine(meta.PositivePrompt);   // "masterpiece, 1girl, solo, ..."
Console.WriteLine(meta.NegativePrompt);
Console.WriteLine($"{meta.ModelName} {meta.Sampler}/{meta.Scheduler} steps={meta.Steps} cfg={meta.Cfg} seed={meta.Seed}");
foreach (var lora in meta.Loras)
    Console.WriteLine($"{lora.Name} ({lora.StrengthModel}/{lora.StrengthClip})");

// PromptTagExtractor — ポジティブプロンプト等をギャラリー絞り込み用の正規化タグ列へ
IPromptTagExtractor extractor = new PromptTagExtractor();
IReadOnlyList<string> tags = extractor.ExtractTags(meta.PositivePrompt);
// "(best quality:1.2), 1girl, <lora:foo:0.8>, ((detailed))" → ["best quality", "1girl", "detailed"]
```

- `KSampler` 系ノードの `positive` / `negative` 入力（`SamplerCustomAdvanced` の `guider` 経由も対応）を辿って `CLIPTextEncode` 系から本文を取得する。リンクを辿り切れない構成では全 `CLIPTextEncode` の `text` を連結してポジ扱いとし `ParseStatus = Partial` を返す
- メタデータが皆無なら `None`。ファイル不存在・ファイル読み取り自体の失敗のみ `ComfyUIException` を送出する（構造が想定外なだけなら例外にせず `Partial` / `None`）
- `PromptTagExtractor` は `<lora:...>` 等を除去（LoRA はタグにしない）、`(tag:1.2)` / `(tag)` / `[tag]` を再帰的に剥がす（エスケープ `\(` は囲みとみなさない）、`BREAK` を除去、大文字小文字を無視して重複排除（初出の表記を採用）。`embedding:xxx` は保持する

### WD14 Tagger（ComfyUI 経由）

```csharp
var tagger = new Wd14TaggerRunner("workflow_config.json");
var imageData = File.ReadAllBytes("input.png");
var tags = await tagger.TagAsync(imageData);
// tags: "1girl, solo, smile, ..."
```

### wdv3-timm（ローカルプロセス経由、ComfyUI 不要）

`WdV3TimmTaggerRunner` はモデル名・しきい値を自分では持たず `wd14_tagger` セクションを共用するため、
`workflow_config.json` に `wd14_tagger`（`model_name` は `WdV3TimmModelMap` で変換できる値であること。
上記「バリデーションルール」節の対応表を参照）を設定しておく必要がある。実行ファイル
（`wdv3_timm.exe`）は `WdV3TimmPaths.ExeFilePath`（利用側アプリの実行ファイルと同じ階層の
`wdv3-timm\wdv3_timm.exe`）から起動する固定パスのため、config ファイル側での指定は不要。

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

wdv3_timm.py 側はタグ名のアンダースコアを保持したまま返す仕様（プロトコル契約）のため、
`WdV3TimmTaggerRunner.TagAsync` が受け取った応答をそのまま返すと `blue_eyes` のように
アンダースコア区切りのタグになってしまう。`Wd14TaggerRunner`（ComfyUI 経由、WD Timm Tagger
カスタムノードが既にアンダースコアを半角スペースへ変換した状態で返す）とタグの見た目を揃えるため、
`WdV3TimmTaggerRunner` は応答受信時に各タグ名のアンダースコアを半角スペースへ変換してから返す
（例: `blue_eyes` → `blue eyes`）。ただし `^_^`/`;_;`/`>_<` のような顔文字系タグ（長さ3文字以下）は
変換すると意味が壊れるため、WD14 Tagger 系ツールで一般的な慣習に合わせて対象外とする（保持される）。

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
| `Ui/UIItemBaseModelTests.cs` | 17 | アイテムリスト管理（Init/Add/Clear）・選択インデックス |
| `Common/JsonLoaderTests.cs` | 13 | JSON 読み書き・エラーハンドリング |
| `Common/SettingTests.cs` | 9 | 設定の永続化・読み込み |
| `Exceptions/ComfyUIExceptionTests.cs` | 3 | ComfyUIException の構築・継承 |
| `Services/ConfigLoaderTests.cs` | 48 | 正常系・異常系のバリデーション（WdV3TimmTaggerRunner はモデル名マッピングのみ検証、wdv3_timm セクション自体は廃止） |
| `Services/ComfyUIClientTests.cs` | 15 | FakeHttpMessageHandler によるモック（GetImageAsync・キャンセル伝播を含む） |
| `Services/WorkflowBuilderTests.cs` | 20 | テンプレート選択・適用（filename_prefix 上書きを含む） |
| `Services/WorkflowRunnerTests.cs` | 15 | FakeComfyUIClient によるモック（outputs 空リトライ・filenamePrefix 伝播・キャンセルを含む） |
| `Services/Wd14TaggerRunnerTests.cs` | 11 | タグ取得フロー・PrependTags/ExcludeTags・タグ取得リトライ |
| `Services/PromptTagExtractorTests.cs` | 16 | カンマ分割・重み記法/ネスト強調の剥がし・LoRA 除去・embedding 保持・BREAK 除去・大文字小文字無視の重複排除・エスケープ括弧・空白畳み込み |
| `Services/ImageMetadataReaderTests.cs` | 13 | tEXt チャンク付き PNG をヘルパー生成して検証。標準 t2i・LoRA 収集・リンク先 Primitive ノード解決・noise_seed・PNG 実寸フォールバック・カスタムサンプラー時の Partial・workflow のみ・メタ無し None・A1111 parameters・prompt 優先・ファイル不存在の例外・非 PNG |
| `Services/WdV3TimmTaggerRunnerTests.cs` | 19 | FakeWdV3TimmProcessClient によるモック（設定バリデーション・遅延プロセス起動・起動引数（WdV3TimmPaths.ExeFilePath 固定）・一時ファイル・応答解釈・タグのアンダースコア→半角スペース正規化（顔文字系タグは保持）・DisposeAsync） |
| `Services/WdV3TimmModelMapTests.cs` | 9 | wd14_tagger.model_name ⇔ wdv3-timm --model の対応表の変換・一覧取得・大文字小文字無視・未知モデル名の挙動 |
| `Services/CaptioningServiceTests.cs` | 14 | タグフィルタ・ディレクトリ一括処理（再帰/上書き/エラー継続/進捗通知）・タグ集計レポート・ITaggerRunner 抽象の直接実装との組み合わせ |
| `Services/PreviewImageCacheServiceTests.cs` | 11 | 画像判定・キャッシュヒット/新規取得/失敗時の挙動 |
| `Models/TagResultTests.cs` | 3 | デフォルト値・シリアライズ/デシリアライズ |
| `Resources/MessagesTests.cs` | 6 | ja/en/en-US でのメッセージ解決・書式指定・未知キーの挙動 |

合計: **261 件**

---

## ライセンス

[LICENSE](LICENSE) を参照してください。
