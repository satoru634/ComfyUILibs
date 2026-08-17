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

## フェーズ3: CaptioningService の新設（`feature/captioning-service` ブランチ、実装完了）

利用側プロジェクト [ComfyUICaptioningTool](https://github.com/satoru634/ComfyUICaptioningTool) の実装ロードマップ フェーズ1（ロジック配置の検討・ComfyUILibs の拡張）に対応。Python 版 `captioning_tool.py` の `CaptioningTool` クラス相当（ディレクトリ走査・タグフィルタ・タグ集計レポート）を UI 非依存のビジネスロジックとして本ライブラリに新設した。

- [x] `Models/CaptioningProgress.cs` — `CaptioningResult`（Processed/Skipped/Error）列挙体と、`IProgress<CaptioningProgress>` 経由で通知する進捗レコードを追加
- [x] `Services/CaptioningService.cs` — `Wd14TaggerRunner` をコンストラクター経由で受け取り、以下を提供
  - `ProcessDirectoryAsync` — ディレクトリ内画像（`.jpg`/`.jpeg`/`.png`/`.webp`、再帰対応）を順次タグ付けし、同名 `.txt` に書き込む。1 ファイルごとに `IProgress<CaptioningProgress>` で通知。画像 1 枚の処理中の例外はすべて捕捉して `Error` として継続する（バッチ全体を止めるのはディレクトリ不存在の場合のみ）
  - `GenerateReportAsync` — ディレクトリ内の全 `.txt`（`tags_report.txt` 自身は除外）を集計し、出現回数の多い順（同数はアルファベット順）で `tags_report.txt` に出力
  - `ApplyTagFilters`（internal） — exclude 除去 → prepend 重複除去 → prepend 挿入、の順でタグ文字列をフィルタ
- [x] 設計判断: サービス自体は `config.json`/`workflow_config.json` 相当の設定ファイルを読み込まない。prepend/exclude タグの union（設定ファイル値と追加指定値の合算）は呼び出し側（GUI 等）が解決してからコンストラクターに渡す方針とした（利用側ごとに設定の持ち方が異なるため）
- [x] `Resources/Messages.resx`/`Messages.en.resx` に `CaptioningService_DirectoryNotFound_Format` を追加
- [x] `ComfyUILibsTests/Services/CaptioningServiceTests.cs`（13件、タグフィルタ・ディレクトリ一括処理の再帰/上書き/エラー継続/進捗通知・タグ集計レポートを検証）を新規作成、全件パス確認済み
- [x] `README.md`/`doc/README_english.md`/`doc/class_diagram.md` を更新

## フェーズ4: WorkflowConfig への prepend_tags/exclude_tags 追加（`feature/prepend-exclude-tags-in-config` ブランチ、実装完了）

利用側プロジェクト [ComfyUICaptioningTool](https://github.com/satoru634/ComfyUICaptioningTool) 側で、既定 prepend/exclude タグの保持先を `AppConfig`（GUI 側の設定ファイル）から `captioning_config.json`（本ライブラリが読み込む設定ファイル）へ一本化するための変更。

- [x] `Models/WorkflowConfig.cs` — `PrependTags`/`ExcludeTags`（`List<string>?`、JSON プロパティ名 `prepend_tags`/`exclude_tags`）を追加。バリデーション対象外（`ConfigLoader`/`ValidateWd14TaggerConfig` は変更なし）
- [x] `Services/Wd14TaggerRunner.cs` — `PrependTags`/`ExcludeTags`（`IReadOnlyList<string>`）を公開プロパティとして追加。設定にキーが存在しない場合は空リストを返す
- [x] `ComfyUILibsTests/Services/Wd14TaggerRunnerTests.cs` に4件追加（値あり/キー欠落 × PrependTags/ExcludeTags）、全件パス確認済み（合計179件）
- [x] `README.md`/`doc/README_english.md`/`doc/class_diagram.md` を更新

## フェーズ5: Wd14TaggerRunner のタグ取得リトライ（`fix/wd14-tagger-output-retry` ブランチ、実装完了）

利用側プロジェクト [ComfyUICaptioningTool](https://github.com/satoru634/ComfyUICaptioningTool) でのディレクトリ一括タグ付け実行時、数枚に1枚程度の頻度で `Wd14TaggerRunner_OutputNotFound`（「WD Timm Tagger の出力が取得できませんでした」）エラーが発生する不具合を修正した。

- **原因**: `ComfyUIClient.MonitorAsync` が `execution_success`/`execution_complete` の WebSocket メッセージ受信、または `IsCompletedAsync` による history キー存在確認の直後に即座に完了とみなして返る一方、ComfyUI サーバー側は WebSocket メッセージ送信後に history への書き込み（`PromptQueue.task_done`）を行う実装のため、メッセージ受信直後に `GetHistoryAsync` を呼ぶと history の `outputs` フィールドがまだ反映されていないことがある（競合状態）。`WorkflowRunner.ExecuteAsync` の `GetOutputsAsync` は既にこの事象を認識してリトライ処理（`MaxOutputsRetryCount`＝3回、`OutputsRetryDelay`＝300ms）を実装済みだったが、`Wd14TaggerRunner.ExtractTagsAsync` には同様のリトライが実装されておらず、初回の `GetHistoryAsync` が空振りした場合に即エラーとなっていた
- [x] `Services/Wd14TaggerRunner.cs` の `ExtractTagsAsync` に `WorkflowRunner` と同じリトライ処理（`MaxExtractRetryCount`＝3回、`ExtractRetryDelay`＝300ms）を追加。タグが取得できるまで、または上限に達するまで `GetHistoryAsync` を再試行する
- [x] `ComfyUILibsTests/Services/Wd14TaggerRunnerTests.cs` に `DelayedHistoryTaggerClient`（指定回数だけ空の history を返すフェイククライアント）を追加し、「リトライ後に成功」「リトライ上限超過でエラー」の2件を新規作成。全件パス確認済み（合計181件）
- [x] `README.md`/`doc/README_english.md` を更新
- **注記**: 本修正は当初 `../ComfyUIRunWorkflow/ComfyUILibs/`（別リポジトリの submodule 実体）側で先に実装したが、`ComfyUICaptioningTool` のビルド参照先が実際には本リポジトリ（`ComfyUICaptioningTool/ComfyUILibs/`）側であると判明したため、同内容をこちらにも反映した（`ComfyUICaptioningTool.sln`/`ComfyUICaptioningTool.csproj` の参照パス誤りは利用側で修正済み）

## フェーズ6: filename_prefix 上書き対応（`feature/filename-prefix` ブランチ、実装完了）

利用側プロジェクト [ComfyUIRunWorkflow](https://github.com/satoru634/ComfyUIRunWorkflow) の DashboardPage/QueuePage に、生成画像の出力ファイル名プレフィックス（ComfyUI の `SaveImage` ノードの `filename_prefix`）を GUI から指定できるテキストボックスを追加するための変更。

- [x] `Services/WorkflowBuilder.cs` — `Apply` に `string? filenamePrefix = null` 引数を追加。非 null かつ空白以外の場合のみ、ワークフロー内の `class_type` が `SaveImage` の全ノードの `inputs.filename_prefix` を上書きする（`ApplyFilenamePrefix` として新設）。null または空白のみの場合はテンプレートに記述された値をそのまま使用する
- [x] `Services/WorkflowRunner.cs` — `ExecuteAsync` に `string? filenamePrefix = null` 引数を追加し、`WorkflowBuilder.Apply` へそのまま渡すよう変更
- [x] `ComfyUILibsTests/Services/WorkflowBuilderTests.cs` — `MinimalTemplateJson` に `SaveImage` ノードを追加し、上書き時／null・空文字・空白時にテンプレート既定値が保持されることを検証するテストを追加
- [x] `ComfyUILibsTests/Services/WorkflowRunnerTests.cs` — `FakeComfyUIClient` に `LastSubmittedWorkflow` を追加し、`ExecuteAsync` の `filenamePrefix` が実際に送信されるワークフロー JSON に反映される／されないことを検証するテストを追加。全件パス確認済み（合計187件）
- [x] `README.md`/`doc/README_english.md`/`doc/class_diagram.md` を更新

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

## フェーズ7: wdv3-timm ローカルプロセス版タグ付けランナーの追加（`feature/wdv3-timm-tagger-runner` ブランチ、実装完了）

利用側プロジェクト [ComfyUICaptioningTool](https://github.com/satoru634/ComfyUICaptioningTool) の GUI は現状 ComfyUI（WD Timm Tagger カスタムノード）経由のタグ付けのみに対応しているが、ComfyUI を使わずローカルスクリプト `wdv3-timm`（`E:\Python_project\wdv3-timm`、timm ライブラリで WD Tagger V3 を実行する単一スクリプトのサンプルリポジトリ）を直接使う別ツールを検討している。GUI（View/ViewModel）の大半は共通化しつつバックエンドだけ差し替えられるよう、本フェーズで `CaptioningService` が `Wd14TaggerRunner` の具象型ではなく抽象インターフェース経由でタグ取得を行うようリファクタリングし、wdv3-timm 向けの新しい `ITaggerRunner` 実装を追加した。

- **性能面の設計判断（ユーザー確認済み）**: `wdv3_timm.py` は実行のたびにモデルをロードする単発 CLI のため、画像 1 枚ごとにプロセスを起動するとモデル再ロードのオーバーヘッドで実用的な速度が出ない。3 案（(1) wdv3-timm 側に常駐サーバーモードを追加、(2) ディレクトリ単位で 1 回だけプロセスを起動、(3) 画像 1 枚ごとにプロセスを起動する簡易実装）を提示し、(1) 常駐サーバーモード方式を採用した。これにより `CaptioningService`/`ITaggerRunner` の「画像 1 枚単位」というインターフェース設計は変更せずに済んでいる
- [x] `Services/ITaggerRunner.cs`（新設） — `PrependTags`/`ExcludeTags`/`TagAsync(byte[], string)` を持つインターフェース。`CaptioningService` はこれ経由でタグ取得を行い、バックエンドの違いを意識しない
- [x] `Services/Wd14TaggerRunner.cs` — `ITaggerRunner` を実装するよう変更（ロジック変更なし、`: ITaggerRunner` を追加しただけ）
- [x] `Services/CaptioningService.cs` — コンストラクター引数・フィールドの型を `Wd14TaggerRunner` → `ITaggerRunner` に変更（`Wd14TaggerRunner` は `ITaggerRunner` を実装するため、既存の呼び出しコードは変更不要）
- [x] `Models/WorkflowConfig.cs` — `WdV3TimmConfig`（`exe_path`/`model`/`general_threshold`/`character_threshold`）と `WorkflowConfig.WdV3Timm`（JSON キー `wdv3_timm`）を追加。`wd14_tagger` と `wdv3_timm` は排他ではなく、使用するバックエンドに応じて必要な方のセクションのみを用意すればよい設計とした
- [x] `Services/ConfigLoader.cs` — `ValidateWdV3TimmConfig`（exe_path 空チェック・model が既知の5種のいずれか・しきい値 0.0〜1.0）と `LoadWdV3TimmConfig`（comfyui_url 不要でロードするだけの薄いラッパー）を追加
- [x] `Services/IWdV3TimmProcessClient.cs`（新設、テスト境界） — wdv3-timm 常駐サーバープロセスとの標準入出力通信を抽象化。**プロトコル契約を XML ドキュメントコメントに明記**（後日 wdv3-timm 側の `--serve` モード実装がこれに従う想定）:
  - 起動: `<exe_path> --serve --model <model> -g <general_threshold> -c <character_threshold>`
  - モデルロード完了後、標準出力に 1 行だけ `{"status":"ready"}` を出力する（進捗ログ等は標準エラー出力に書き、標準出力はプロトコル専用とする）
  - リクエスト: 標準入力へ 1 行 `{"image_path":"<絶対パス>"}` を書き込み flush
  - 応答: 標準出力へ 1 行、成功時 `{"status":"ok","tags":"1girl, blue_eyes, solo"}`、失敗時 `{"status":"error","message":"..."}`
  - 終了: クライアントが標準入力を閉じる（EOF）とサーバーは処理中のリクエストを終えてから終了する
- [x] `Services/WdV3TimmProcessClient.cs`（新設） — `IWdV3TimmProcessClient` の既定実装。`System.Diagnostics.Process` で起動し、`StartAsync` で ready シグナルを待機、`DisposeAsync` で標準入力を閉じてグレースフル終了を待ち（タイムアウト 5 秒）、超過時は `Kill(entireProcessTree: true)` で強制終了する
- [x] `Services/WdV3TimmTaggerRunner.cs`（新設） — `ITaggerRunner`/`IAsyncDisposable` を実装。初回 `TagAsync` 呼び出し時にサーバープロセスを遅延起動し（`SemaphoreSlim` で多重起動防止）、以降の呼び出しは同じプロセスを使い回す。`TagAsync` は画像バイト列を一時ファイル（`Path.GetTempPath()`、拡張子はファイル名から判定・省略時は `.png`）へ書き出し、そのパスをリクエストとして送信、応答受信後は一時ファイルを削除する（成功・失敗いずれの場合も `finally` で削除）
- [x] `Resources/Messages.resx`/`Messages.en.resx` — `ConfigLoader_WdV3Timm*`（5件）・`WdV3TimmTaggerRunner_*`（5件）を追加
- [x] `ComfyUILibsTests/Services/WdV3TimmTaggerRunnerTests.cs`（新設、16件） — `FakeWdV3TimmProcessClient`（起動引数・リクエスト・キュー済み応答を記録するモック）を使用。設定バリデーション・`PrependTags`/`ExcludeTags`・初回呼び出し時の遅延プロセス起動と起動引数の検証・2 回目以降は再起動しないこと・一時ファイルへの書き込みと（成功/失敗いずれの場合も）削除・正常応答/エラー応答/EOF（プロセス予期せぬ終了）/不正 JSON 応答それぞれの解釈・`DisposeAsync` の挙動（未起動時は何もしない／起動済みならプロセスクライアントを終了する）を検証
- [x] `ComfyUILibsTests/Services/ConfigLoaderTests.cs` に `ValidateWdV3TimmConfig`/`LoadWdV3TimmConfig` のテストを14件追加
- [x] `ComfyUILibsTests/Services/CaptioningServiceTests.cs` に、`Wd14TaggerRunner` を経由しない `ITaggerRunner` の直接実装（`FakeTaggerRunner`）と組み合わせても `CaptioningService` が正しく動作することを検証するテストを1件追加（抽象化のリグレッションテスト）
- 全件パス確認済み（`ComfyUILibsTests.exe` 直接実行で確認。187件 → 218件）
- `README.md`/`doc/README_english.md`/`doc/class_diagram.md`/本ファイルを更新
- **本フェーズのスコープ外**: wdv3-timm リポジトリ（`E:\Python_project\wdv3-timm`）側の `--serve` 常駐サーバーモード実装は別タスク（プロトコル契約は上記の通り本フェーズで確定済み）。利用側プロジェクト（ComfyUICaptioningTool）の GUI 配線（バックエンド選択 UI・`ConfigPage` への `wdv3_timm` セクション編集追加等）も別タスク

## テスト（ComfyUILibsTests）

各クラスに対応するテストを `ComfyUILibsTests/<同じ名前空間>/` に配置済み。件数の内訳は `README.md` の「テスト」セクション参照（全パス）。

## 利用側での経緯

- WPF GUI（ComfyUIRunWorkflow）フェーズ2〜7 の実装経緯は、そちら側のリポジトリの `.claude/implementation_status.md` を参照。

## 将来的な拡張

- C# 版 Discord ボット（本リポジトリを共用）からの利用
- 実行履歴の永続化（SQLite 等）
