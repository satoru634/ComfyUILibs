# ディレクトリ構成

```
ComfyUILibs/                            <- リポジトリルート
  ComfyUILibs/
    Base/                                <- 汎用ジオメトリ値ラッパー（INotifyPropertyChanged 対応）
      ObservablePoint.cs                 <- Point ラッパー（ToPoint / FromPoint）
      ObservableSize.cs                  <- Size ラッパー（ToSize / FromSize）
    Ui/                                  <- UI 選択リスト管理用の汎用ベースクラス（WPF ComboBox 等／将来の Discord ボット選択メニューでも共用想定）
      UIItemBaseModel.cs                 <- アイテムリスト＋選択インデックスを管理するジェネリッククラス（Init/Add/Clear）
    Common/                              <- 汎用ユーティリティ
      JsonLoader.cs                      <- JSON ファイル読み書きユーティリティ（静的クラス）
      Setting.cs                         <- 設定ファイル管理ジェネリッククラス
    Exceptions/                          <- 独自例外クラス群
      ComfyUIException.cs                <- 基底例外クラス
    Models/                              <- データモデル
      WorkflowConfig.cs                  <- workflow_config.json モデル（ImageSize, LoraEntry, WorkflowSettings, Wd14TaggerConfig, WorkflowConfig）
      WorkflowInput.cs                   <- 入力 JSON モデル（PromptPair, WorkflowInput）
      WorkflowResult.cs                  <- 結果モデル（OutputFile, WorkflowParameters, WorkflowResult）
      ResolvedLora.cs                    <- LoRA 解決済みエントリ
      TagResult.cs                       <- WD14 Tagger 実行結果モデル（tag_result_*.json 用）
    Services/                            <- ComfyUI API 通信・ワークフロー制御ロジック
      IComfyUIClient.cs                  <- ComfyUIClient インターフェース（DI / テスト用、GetImageAsync を含む）
      ComfyUIClient.cs                   <- comfyui_client.py の移植（GET /view による画像取得を含む）
      WorkflowBuilder.cs                 <- workflow_builder.py の移植
      WorkflowRunner.cs                  <- WorkflowRunner クラスの移植
      Wd14TaggerRunner.cs                <- wd14_tagger_runner.py の移植
      ConfigLoader.cs                    <- load_files.py の移植
      IPreviewImageCacheService.cs       <- プレビュー画像キャッシュのインターフェース（DI / テスト用）
      PreviewImageCacheService.cs        <- 生成画像プレビューのローカルキャッシュ管理（GET /view 結果をファイルキャッシュ）
    Properties/
      AssemblyInfo.cs                    <- InternalsVisibleTo("ComfyUILibsTests") を宣言
  ComfyUILibsTests/                      <- xUnit テストプロジェクト
    Base/
      ObservablePointTests.cs
      ObservableSizeTests.cs
    Ui/
      UIItemBaseModelTests.cs
    Common/
      JsonLoaderTests.cs
      SettingTests.cs
    Exceptions/
      ComfyUIExceptionTests.cs
    Models/
      TagResultTests.cs
    Services/
      ConfigLoaderTests.cs
      ComfyUIClientTests.cs
      WorkflowBuilderTests.cs
      WorkflowRunnerTests.cs
      Wd14TaggerRunnerTests.cs
      PreviewImageCacheServiceTests.cs
  doc/
    README_english.md                   <- README.md の英語版
    class_diagram.md                    <- Mermaid 記法によるクラス図
```

## 利用側リポジトリでの参照

このリポジトリは Git submodule として以下のプロジェクトから参照される想定。

- `ComfyUIRunWorkflow/ComfyUILibs/` — WPF GUI（[ComfyUIRunWorkflow](https://github.com/satoru634/ComfyUIRunWorkflow)）
- （作成予定）C# Discord ボットリポジトリ配下 — Discord ボット
