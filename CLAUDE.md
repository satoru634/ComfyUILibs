# プロジェクト概要

ComfyUI API 通信・ワークフロー制御・設定管理などのビジネスロジックを提供する .NET 8 クラスライブラリ。
[comfyui_tools](https://github.com/satoru634/comfyui_tools) の Python 実装を C# に移植したもの。

WPF GUI（[ComfyUIRunWorkflow](https://github.com/satoru634/ComfyUIRunWorkflow)、本リポジトリを Git submodule として参照）と、将来の C# Discord ボット（別リポジトリで作成予定）の両方から共用されることを前提に設計されている。**UI・プレゼンテーション層のコードは一切含まない。**

## 詳細ドキュメント

タスク開始前に、関連するドキュメントを確認すること。

- 実装状況: @.claude/implementation_status.md
- 技術スタック: @.claude/tech_stack.md
- ディレクトリ構成: @.claude/directory_structure.md
- Common ユーティリティ（JsonLoader / Setting）の詳細: @.claude/comfyuilibs_common.md
- クラス図: @doc/class_diagram.md
- 機能一覧・使い方: @README.md

## 開発ルール

- ファイルの変更や追加を行う前に、作業ブランチを切ること。
- クラスを追加・変更したら、対応するユニットテストを追加すること（テストフレームワーク: xUnit、配置先: `ComfyUILibsTests/<同じ名前空間>/`）
- ユニットテストがパスするまで次の実装に進まないこと。
- 指示があるまでコミットしないこと。
- 実装後は `README.md` および `doc/README_english.md` を更新する。
- クラスの新規追加・変更があった場合は、クラス図（@doc/class_diagram.md）も変更すること。
- ファイルやディレクトリ構成を変更した場合は、CLAUDE.md および `.claude` 配下に記載の該当箇所も変更する。
- プルリクマージ後は、作業ブランチをローカル・リモート共に削除し、master ブランチを最新にする。
- **本ライブラリを利用する側（WPF GUI・Discord ボット等）固有の実装は含めない。** UI・プレゼンテーション層のロジックが必要な場合は利用側プロジェクトに実装する。

## コーディング規約

- 非同期メソッドは必ず `async`/`await` を使用（`Task.Result` / `.Wait()` 禁止）
- nullable 有効化済み（`#nullable enable`）
- 例外は独自例外クラスで統一（`Exceptions/` 参照、基底クラス `ComfyUIException`）
- Python版の `ValueError` に相当するものは `ComfyUIException` などプロジェクト固有例外にマップする
- WebSocket 受信ループではバイナリフレームを必ずスキップすること（ComfyUI がプレビュー画像をバイナリで送信するため）
- `WorkflowBuilder` でテンプレートを書き換える際は必ず DeepCopy/Clone してから行うこと

## ComfyUI API 概要

- `POST /prompt` — ワークフロー送信、`prompt_id` 取得
- `GET /history/{prompt_id}` — 実行結果・出力ファイル一覧取得
- `POST /upload/image` — 画像アップロード（WD14 Tagger 用）
- `ws://host/ws?clientId={uuid}` — 実行進捗の WebSocket 監視
  - `execution_complete` → 正常完了
  - `execution_error` → エラー
  - `executing` (node=null) → 古い ComfyUI の完了シグナル（ポーリングへフォールバック）

## 利用側プロジェクト

| プロジェクト | 種別 | 参照方法 |
|---|---|---|
| [ComfyUIRunWorkflow](https://github.com/satoru634/ComfyUIRunWorkflow) | WPF GUI | `ComfyUIRunWorkflow/ComfyUILibs/` に Git submodule として配置 |
| C# Discord ボット（開発予定） | Discord ボット | 別リポジトリから本リポジトリを Git submodule として参照予定 |

新しい利用側プロジェクトを追加する場合も、本ライブラリには手を加えず DI 経由で `Services/` 配下のクラスを利用すること。
