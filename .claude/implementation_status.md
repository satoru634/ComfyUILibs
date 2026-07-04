# 実装状況

ComfyUILibs は Python 版 [comfyui_tools](https://github.com/satoru634/comfyui_tools) の `run_workflow` 相当のビジネスロジックを C# に移植したクラスライブラリ。
フェーズ1（本ライブラリの実装）は完了・master マージ済み。現在は WPF GUI（ComfyUIRunWorkflow）から利用されている。
フェーズ2（例外メッセージの多言語化、`feature/i18n-messages` ブランチ）が実装完了。ComfyUIRunWorkflow 側の GUI 多言語化（フェーズ9、そちら側の実装状況ドキュメント参照）は本フェーズのマージ後に着手予定。

現在のクラス一覧・使い方は `README.md`（機能一覧・使い方・テンプレート仕様）を、テスト件数の内訳は `README.md` の「テスト」セクションを参照。実装が進むたびに更新される。

## 実装済みコンポーネント

**Base**
- `Base/ObservablePoint.cs` — `INotifyPropertyChanged` 対応の Point ラッパー
- `Base/ObservableSize.cs` — `INotifyPropertyChanged` 対応の Size ラッパー

**Ui**
- `Ui/UIItemBaseModel.cs` — アイテムリスト＋選択インデックス管理の汎用ジェネリッククラス（WPF ComboBox 等 UI 選択リスト向け。将来の Discord ボットの選択メニューでも共用想定）

**Common**
- `Common/JsonLoader.cs` — JSON ファイル読み書き静的ユーティリティ
- `Common/Setting.cs` — 設定ファイル永続化ジェネリッククラス

**Exceptions**
- `Exceptions/ComfyUIException.cs` — 基底例外クラス

**Resources**
- `Resources/Messages.resx` / `Messages.en.resx` / `Messages.cs` — 例外メッセージの多言語化リソース（既定: 日本語、`CultureInfo.CurrentUICulture` に応じて英語に切替）

**Models**
- `Models/WorkflowConfig.cs` — 設定 JSON モデル（WorkflowConfig, LoraEntry, ImageSize, WorkflowSettings, Wd14TaggerConfig）
- `Models/WorkflowInput.cs` — 入力 JSON モデル（WorkflowInput, PromptPair）
- `Models/WorkflowResult.cs` — 結果モデル（WorkflowResult, OutputFile, WorkflowParameters）
- `Models/ResolvedLora.cs` — LoRA 解決済みエントリ
- `Models/TagResult.cs` — WD14 Tagger 実行結果モデル（tag_result_*.json 用）

**Services**
- `Services/IComfyUIClient.cs` / `ComfyUIClient.cs` — REST API + WebSocket クライアント（comfyui_client.py 移植）
- `Services/ConfigLoader.cs` — workflow_config.json ロード・バリデーション（load_files.py 移植）
- `Services/WorkflowBuilder.cs` — テンプレート選択・書き換え（workflow_builder.py 移植）
- `Services/WorkflowRunner.cs` — 実行ファサード（WorkflowRunner 移植）
- `Services/Wd14TaggerRunner.cs` — WD14 Tagger（wd14_tagger_runner.py 移植）
- `Services/IPreviewImageCacheService.cs` / `PreviewImageCacheService.cs` — 生成画像プレビューのローカルキャッシュ管理

## フェーズ2: 例外メッセージの多言語化（`feature/i18n-messages` ブランチ、実装完了）

`ComfyUIRunWorkflow` の多言語化（日本語/英語）に伴い、`ComfyUIException` がスローするメッセージを `.resx` ベースのリソースに外部化した。

- [x] `Resources/Messages.resx`（既定・neutral resource、日本語）／`Messages.en.resx`（英語サテライト）を新規作成
- [x] `Resources/Messages.cs` — `System.Resources.ResourceManager` をラップした internal static クラス。`Get(key)` / `Get(key, args...)` で `CultureInfo.CurrentUICulture` に応じたメッセージを取得
- [x] `ComfyUIClient.cs` / `ConfigLoader.cs` / `Wd14TaggerRunner.cs` / `WorkflowBuilder.cs` / `WorkflowRunner.cs` の全 `throw new ComfyUIException("日本語文言")` を `Messages.Get(...)` 参照に置換
- [x] ワークフローテンプレートのノードタイトル（`"positive_prompt"` 等の識別子、`Wd14TaggerRunner` の `"画像を読み込む"` 等）はテンプレート JSON の `_meta.title` と一致させる必要があるため対象外（文言ではなく識別子のため変更しない）
- [x] 既存テストのうち、日本語の厳密な部分文字列に依存していた箇所（`ConfigLoaderTests.cs`・`WorkflowBuilderTests.cs`・`Wd14TaggerRunnerTests.cs`）を `Messages.Get(...)` 参照による比較に置換し、実行環境の OS ロケールに依存しないようにした
  - なお、`WorkflowRunnerTests.cs` や `Wd14TaggerRunnerTests.cs` の一部テストは Fake クライアントが直接返す固定文字列を検証しているだけで `Messages` を経由しないため、変更不要と判断
- [x] `Resources/MessagesTests.cs` を新規作成し、ja/en/en-US カルチャでのメッセージ解決・書式指定・未知キー時の挙動を検証

### 設計上の注意点

- ResourceManager はカルチャに satellite がない場合 neutral resource（既定）にフォールバックするが、`en`/`en-US` の satellite が存在する場合はそちらが優先される。そのため「OS ロケールに関わらず常に日本語をデフォルトにする」という要件は、ComfyUIRunWorkflow 側が起動時に明示的に `CultureInfo.CurrentUICulture` をセットすることで担保する（本ライブラリ側では制御しない）
- 本ライブラリは UI 非依存の方針を維持しつつ、`CultureInfo.CurrentUICulture`（.NET 標準のスレッドローカル設定）を見るだけなので `ComfyUILibs/CLAUDE.md` の「UI・プレゼンテーション層のコードは一切含まない」という制約に抵触しない

## テスト（ComfyUILibsTests）

各クラスに対応するテストを `ComfyUILibsTests/<同じ名前空間>/` に配置済み。件数の内訳は `README.md` の「テスト」セクション参照（全パス）。

## 利用側での経緯

- WPF GUI（ComfyUIRunWorkflow）フェーズ2〜7 の実装経緯は、そちら側のリポジトリの `.claude/implementation_status.md` を参照。

## 将来的な拡張

- C# 版 Discord ボット（本リポジトリを共用）からの利用
- 実行履歴の永続化（SQLite 等）
