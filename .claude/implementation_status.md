# 実装状況

ComfyUILibs は Python 版 [comfyui_tools](https://github.com/satoru634/comfyui_tools) の `run_workflow` 相当のビジネスロジックを C# に移植したクラスライブラリ。
フェーズ1（本ライブラリの実装）は完了・master マージ済み。現在は WPF GUI（ComfyUIRunWorkflow）から利用されている。

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

## テスト（ComfyUILibsTests）

各クラスに対応するテストを `ComfyUILibsTests/<同じ名前空間>/` に配置済み。件数の内訳は `README.md` の「テスト」セクション参照（全パス）。

## 利用側での経緯

- WPF GUI（ComfyUIRunWorkflow）フェーズ2〜7 の実装経緯は、そちら側のリポジトリの `.claude/implementation_status.md` を参照。

## 将来的な拡張

- C# 版 Discord ボット（本リポジトリを共用）からの利用
- 実行履歴の永続化（SQLite 等）
