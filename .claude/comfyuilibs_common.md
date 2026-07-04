# ComfyUILibs.Common — 実装済みユーティリティ

## JsonLoader（静的クラス）

`ComfyUILibs.Common.JsonLoader`

JSON の読み書きを担う静的ユーティリティ。全メソッドに共通するオプション:
- `JavaScriptEncoder.Create(UnicodeRanges.All)` — 日本語等の非 ASCII 文字をエスケープしない
- `WriteIndented = true` — 読みやすいインデント付き出力

### メソッド一覧

| メソッド | 説明 |
|---|---|
| `ReadJson<T>(string path)` | ファイルパスから JSON を読み込み `T` にデシリアライズ |
| `ReadJson<T>(Stream stream)` | ストリームから JSON を読み込み `T` にデシリアライズ |
| `DeserializeJson<T>(string? json)` | JSON 文字列を `T` にデシリアライズ |
| `WriteJson<T>(string path, in T data)` | `T` を JSON にシリアライズしてファイルに書き込む |

### 挙動の注意点

- `T` は `new()` 制約あり（パラメーターなしコンストラクター必須）
- `DeserializeJson` の引数が `null` または デシリアライズ結果が `null` の場合は `new T()` を返す（例外にしない）
- `WriteJson` はディレクトリが存在しない場合に自動作成する
- `WriteJson` は UTF-8 で書き込む（BOM なし）

### 使用場面

```csharp
// 型付きモデルの読み込み（ConfigLoader 等で使用）
var config = JsonLoader.ReadJson<AppConfig>("workflow_config.json");

// 型付きモデルの書き込み（結果保存等で使用）
JsonLoader.WriteJson("result.json", workflowResult);
```

### 注意：ワークフローテンプレートには使用不可

ワークフローテンプレート JSON は `JsonNode` / `JsonObject` として動的に書き換える必要があるが、
`JsonNode` は `new()` 制約を満たさないため `JsonLoader.ReadJson<T>` は使えない。
`WorkflowBuilder` では `JsonNode.Parse(File.ReadAllText(path))` を直接使用すること。

---

## Setting\<T\>（ジェネリッククラス）

`ComfyUILibs.Common.Setting<T>`

JSON ファイルによる設定の永続化を担うラッパークラス。内部で `JsonLoader` を使用する。

### コンストラクター

```csharp
Setting(string settingPath, bool onLoad = true)
```

- `settingPath` — 設定ファイルのパス
- `onLoad = true` — コンストラクター呼び出し時に自動ロードするか

### メソッド・プロパティ

| メンバー | 説明 |
|---|---|
| `T Data` | 設定データ本体（get/set） |
| `Load()` | ファイルが存在しない場合はデフォルト値で `Save()` してから読み込む |
| `Save()` | `Data` を `settingPath` に書き込む |

### 使用場面

```csharp
// GUI の設定（接続先 URL など）を管理する例
var appSettings = new Setting<GuiSettings>("appsettings.json");
appSettings.Data.ComfyuiUrl = "http://127.0.0.1:8188";
appSettings.Save();
```

### ファイルが存在しない場合の挙動

`Load()` 呼び出し時にファイルがない場合 → `new T()` の内容で `Save()` → その値で `Data` を上書き。
初回起動時にデフォルト設定ファイルを自動生成するユースケースに適している。

---

## 設計上の使い分け

| シナリオ | 使用するクラス |
|---|---|
| `workflow_config.json` のロード・バリデーション（`ConfigLoader`内） | `JsonLoader.ReadJson<AppConfig>()` |
| 利用側アプリ独自設定（URL 保存等）の永続化 | `Setting<T>` |
| ワークフローテンプレート JSON の読み込み（`WorkflowBuilder`内） | `JsonNode.Parse(File.ReadAllText(...))` を直接使用 |
| 実行結果 JSON の書き出し | `JsonLoader.WriteJson<WorkflowResult>()` |
