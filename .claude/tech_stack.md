# 技術スタック

- .NET 8 クラスライブラリ（`net8.0-windows7.0`）
- `System.Net.Http.HttpClient` — REST API 呼び出し
- `System.Net.WebSockets.ClientWebSocket` — WebSocket 監視
- `System.Text.Json` — JSON 操作（`JsonLoader` / `Setting<T>` 経由）
- `CommunityToolkit.Mvvm 8.4.2` — `ObservableObject` 基底クラス（`Base/` / `Ui/` の Observable ラッパー用）
- `.resx` + `System.Resources.ResourceManager` — 例外メッセージの多言語化（`Resources/Messages.cs`、`CultureInfo.CurrentUICulture` に連動）

## テスト

- xUnit v3（`ComfyUILibsTests`）
