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
    Resources/                           <- 多言語化用メッセージリソース
      Messages.resx                      <- 例外メッセージ（既定・日本語）
      Messages.en.resx                   <- 例外メッセージ（英語サテライト）
      Messages.cs                        <- CurrentUICulture に応じてメッセージを解決する静的ヘルパー
    Models/                              <- データモデル
      WorkflowConfig.cs                  <- workflow_config.json モデル（ImageSize, LoraEntry, WorkflowSettings,
                                             Wd14TaggerConfig, WorkflowConfig）。wdv3_timm セクション・
                                             WdV3TimmConfig クラスはフェーズ8の追加修正で廃止済み
                                             （wdv3_timm.exe_path は config ファイルでは指定せず、
                                             Services/WdV3TimmPaths.cs の固定パス規約に一本化した）。
                                             WdV3TimmTaggerRunner はモデル名・しきい値を独自に持たず
                                             Wd14TaggerConfig（wd14_tagger セクション）を共用する
      WorkflowInput.cs                   <- 入力 JSON モデル（PromptPair, WorkflowInput）
      WorkflowResult.cs                  <- 結果モデル（OutputFile, WorkflowParameters, WorkflowResult）
      ResolvedLora.cs                    <- LoRA 解決済みエントリ
      TagResult.cs                       <- WD14 Tagger 実行結果モデル（tag_result_*.json 用）
      CaptioningProgress.cs              <- CaptioningService の進捗通知モデル（CaptioningResult 列挙体を含む）
      ComfyImageMetadata.cs             <- 画像埋め込みメタデータの抽出結果モデル（フェーズ11で新設）。
                                             ComfyMetadataParseStatus（Ok/Partial/None）列挙体・
                                             ComfyLoraRef（Name/StrengthModel/StrengthClip）・
                                             ComfyImageMetadata（ポジ/ネガ・生成パラメータ・解像度・
                                             Loras・生 prompt/workflow JSON・ParseStatus）を含む
    Services/                            <- ComfyUI API 通信・ワークフロー制御ロジック
      IComfyUIClient.cs                  <- ComfyUIClient インターフェース（DI / テスト用、GetImageAsync を含む）
      ComfyUIClient.cs                   <- comfyui_client.py の移植（GET /view による画像取得を含む）
      WorkflowBuilder.cs                 <- workflow_builder.py の移植
      WorkflowRunner.cs                  <- WorkflowRunner クラスの移植
      ITaggerRunner.cs                   <- 画像 1 枚のタグ付けランナーを抽象化するインターフェース。
                                             CaptioningService はこれ経由でタグ取得を行い、
                                             ComfyUI 経由か否かを意識しない（Wd14TaggerRunner /
                                             WdV3TimmTaggerRunner の両方が実装する）
      Wd14TaggerRunner.cs                <- wd14_tagger_runner.py の移植。ITaggerRunner を実装
      IWdV3TimmProcessClient.cs          <- wdv3-timm 常駐サーバープロセスとの標準入出力（JSON Lines）
                                             通信を抽象化するインターフェース（DI / テスト用）。
                                             プロトコル契約（起動引数・ready シグナル・リクエスト/応答形式・
                                             終了方法）を XML ドキュメントコメントに明記している
      WdV3TimmProcessClient.cs           <- IWdV3TimmProcessClient の既定実装。System.Diagnostics.Process で
                                             wdv3_timm.exe（WdV3TimmPaths.ExeFilePath が指す固定パス）を
                                             --serve 引数で常駐起動し、標準入出力で 1 行 1 JSON をやり取りする
      WdV3TimmPaths.cs                   <- wdv3_timm.exe の固定パス規約を集約する静的クラス（フェーズ8の
                                             追加修正で新設）。RootDirectory =
                                             Path.Combine(AppContext.BaseDirectory, "wdv3-timm")、
                                             ExeFilePath = Path.Combine(RootDirectory, "wdv3_timm.exe")。
                                             利用側アプリの実行ファイルと同階層の wdv3-timm フォルダに
                                             実行ファイル一式（.venv・wdv3_timm.exe）が配置される前提
                                             （config ファイルでのパス指定は廃止した）
      WdV3TimmModelMap.cs                <- wd14_tagger.model_name（ComfyUI 側、Hugging Face リポジトリ名）と
                                             wdv3-timm の --model 値（wdv3_timm.py の MODEL_REPO_MAP 短縮キー）の
                                             対応表を持つ静的クラス（フェーズ8で新設）。TryGetWdV3TimmModel/
                                             SupportedWdV3TimmModels に加え、利用側 GUI の ConfigPage
                                             モデル名選択 ComboBox が参照する SupportedWd14ModelNames
                                             （wd14_tagger.model_name 側の一覧）を公開する
      WdV3TimmTaggerRunner.cs            <- ITaggerRunner の実装。ComfyUI を経由せず、ローカルの wdv3-timm を
                                             常駐サーバーモードで起動してタグ付けする（画像 1 枚ごとに
                                             プロセスを起動するとモデル再ロードのオーバーヘッドが大きいため、
                                             初回 TagAsync 呼び出し時にプロセスを起動し使い回す設計）。
                                             IAsyncDisposable を実装し、DisposeAsync でプロセスを終了する。
                                             モデル名・しきい値は独自に持たず Wd14Tagger セクションを共用し、
                                             WdV3TimmModelMap で --model 引数へ変換する（フェーズ8）。
                                             起動する実行ファイルパスは WdV3TimmPaths.ExeFilePath（固定、
                                             フェーズ8の追加修正で config 由来の値から変更）。実行ファイル
                                             未配置時の専用チェックは持たず、WdV3TimmProcessClient.StartAsync
                                             内の Win32Exception → ComfyUIException ラップに委ねる。
                                             フェーズ9で、応答に含まれるタグ文字列を NormalizeTags
                                             （private static）で正規化するようにした。wdv3_timm.py の
                                             --serve モードはタグ名のアンダースコアを保持したまま返す
                                             プロトコル契約のため、そのまま返すと Wd14TaggerRunner
                                             （ComfyUI 経由、WD Timm Tagger カスタムノードが既にアンダー
                                             スコアを半角スペースへ変換済み）とタグの見た目が揃わない問題が
                                             あった。各タグ名の長さが3文字を超える場合のみアンダースコアを
                                             半角スペースへ置換する（^_^/;_; のような顔文字系タグは
                                             WD14 Tagger 系ツールの慣習に合わせて対象外とし保持する）
      CaptioningService.cs               <- captioning_tool.py の CaptioningTool クラスの移植
                                             （ディレクトリ走査・タグフィルタ・タグ集計レポート）。
                                             コンストラクター引数は ITaggerRunner（Wd14TaggerRunner の
                                             具象型ではなく抽象に依存する）
      ConfigLoader.cs                    <- load_files.py の移植。ValidateWdV3TimmConfig（ValidateWd14TaggerConfig
                                             で wd14_tagger セクションの妥当性を検証した上で、model_name が
                                             WdV3TimmModelMap で変換可能であることを検証するのみ。フェーズ8の
                                             追加修正で exe_path の検証を削除した）・LoadWdV3TimmConfig
                                             （wdv3-timm 専用ロード、comfyui_url 不要）を含む
      IPreviewImageCacheService.cs       <- プレビュー画像キャッシュのインターフェース（DI / テスト用）
      PreviewImageCacheService.cs        <- 生成画像プレビューのローカルキャッシュ管理（GET /view 結果をファイルキャッシュ）
      IPromptTagExtractor.cs            <- プロンプト文字列 → 正規化タグ列 変換の抽象（フェーズ11で新設）
      PromptTagExtractor.cs             <- IPromptTagExtractor の既定実装。<lora:...> 除去・カンマ分割・
                                             BREAK 除去・重み記法 (tag:1.2)/(tag)/[tag] の再帰剥がし
                                             （エスケープ \( は囲みとみなさない）・空白畳み込み＋エスケープ復元・
                                             大文字小文字無視の重複排除（初出表記を採用）。embedding:xxx は保持
      IImageMetadataReader.cs           <- PNG 埋め込みメタデータ読み取りの抽象（フェーズ11で新設）
      ImageMetadataReader.cs            <- IImageMetadataReader の既定実装。PNG チャンク（tEXt/iTXt/zTXt）を
                                             直接走査し、ComfyUI の prompt（API 形式）を優先解析。KSampler 系の
                                             positive/negative（SamplerCustomAdvanced の guider 経由も）を辿って
                                             CLIPTextEncode 系から本文を取得。checkpoint/UNET ローダから model 名、
                                             EmptyLatentImage から解像度、LoraLoader 系から LoRA を収集。prompt が
                                             無ければ A1111 形式 parameters チャンクをフォールバック解析。
                                             辿り切れない場合は全 CLIPTextEncode の text を連結して Partial、
                                             メタデータ皆無なら None。ファイル不存在・読み取り失敗のみ
                                             ComfyUIException を送出
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
    Resources/
      MessagesTests.cs
    Models/
      TagResultTests.cs
    Services/
      ConfigLoaderTests.cs
      ComfyUIClientTests.cs
      WorkflowBuilderTests.cs
      WorkflowRunnerTests.cs
      PromptTagExtractorTests.cs          <- PromptTagExtractor のテスト（フェーズ11で新設、16件）
      ImageMetadataReaderTests.cs         <- ImageMetadataReader のテスト（フェーズ11で新設、13件。
                                             tEXt チャンク付き PNG をテスト内ヘルパーで生成して検証）
      Wd14TaggerRunnerTests.cs
      WdV3TimmTaggerRunnerTests.cs        <- WdV3TimmTaggerRunner のテスト（FakeWdV3TimmProcessClient による
                                             モック。設定バリデーション・PrependTags/ExcludeTags・初回呼び出し時の
                                             遅延プロセス起動と起動引数・一時ファイルへの書き込みと削除・
                                             正常/エラー/EOF/不正JSON 応答の解釈・DisposeAsync の挙動・
                                             wd14_tagger セクション欠落/モデル名未対応時の例外送出（フェーズ8）を検証。
                                             フェーズ8の追加修正で、起動時に渡される実行ファイルパスが
                                             WdV3TimmPaths.ExeFilePath と一致することを検証するよう変更した）
      WdV3TimmModelMapTests.cs            <- WdV3TimmModelMap のテスト（フェーズ8で新設。5モデルの変換・
                                             大文字小文字無視・未知モデル名時の false/null・
                                             SupportedWdV3TimmModels/SupportedWd14ModelNames の内容を検証）
      CaptioningServiceTests.cs           <- FakeTaggerRunner（Wd14TaggerRunner を経由しない ITaggerRunner の
                                             直接実装）を使ったテストを含み、CaptioningService が
                                             ITaggerRunner 抽象のみに依存していることを検証する
      PreviewImageCacheServiceTests.cs
  doc/
    README_english.md                   <- README.md の英語版
    class_diagram.md                    <- Mermaid 記法によるクラス図
```

## 利用側リポジトリでの参照

このリポジトリは Git submodule として以下のプロジェクトから参照される想定。

- `ComfyUIRunWorkflow/ComfyUILibs/` — WPF GUI（[ComfyUIRunWorkflow](https://github.com/satoru634/ComfyUIRunWorkflow)）
- （作成予定）C# Discord ボットリポジトリ配下 — Discord ボット
