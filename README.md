# MatrixTea Engine

<img src="branding/matrixtea.svg" alt="MatrixTea" width="96" />

MatrixTea Engine 由 MoriTeahouse（森之宿茶室）開發，提供固定步進、場景生命週期、輸入事件緩衝、節奏判定、音訊時間軸、冒險碰撞與任務、原子存檔及 ATR 遊戲封裝。圖形接入層使用 MonoGame DesktopGL；核心與封裝模組可獨立使用。

## 模組

| 專案 | 目標框架 | 職責 |
|---|---|---|
| [MatrixTea.Engine.Core](src/MatrixTea.Engine.Core) | .NET 6 / .NET 8 | 計時、固定步進、場景、服務、診斷、節奏與通用檔案寫入 |
| [MatrixTea.Engine.MonoGame](src/MatrixTea.Engine.MonoGame) | .NET 6 / .NET 8 | MonoGame 視窗、圖形裝置與引擎循環接入 |
| [MatrixTea.Engine.Desktop](src/MatrixTea.Engine.Desktop) | .NET 8 / Windows | 裝置播放位置、串流音訊與中文字形快取 |
| [MatrixTea.Engine.Packaging](src/MatrixTea.Engine.Packaging) | .NET 8 | ATR1 讀寫、壓縮與 AES-GCM 驗證 |
| [MatrixTea.Packaging.Cli](src/MatrixTea.Packaging.Cli) | .NET 8 | 命令列封裝工具 |
| [MatrixTea.Editor.Core](src/MatrixTea.Editor.Core) | .NET 8 | 專案資料、交易歷史、引擎預覽與內容匯出 |
| [MatrixTea.Editor](src/MatrixTea.Editor) | .NET 8 / Windows | MatrixTea Studio 可視化地圖、物件、任務與四軌譜面編輯器 |

Core 不依賴 MonoGame。遊戲負責內容、輸入映射、UI、網路與平台整合。Desktop 是 Windows 專用的選用模組。MatrixTea Studio 0.1.0-test.1 提供可視化編輯、復原／重做、存讀、實際遊玩預覽與 ATR 內容匯出；Windows 發布包自帶 .NET 8。操作與首版範圍見 [編輯器指南](docs/editor.md)。

```powershell
dotnet run --project src/MatrixTea.Editor -c Release
dotnet run --project tests/MatrixTea.Editor.Tests -c Release
pwsh -File scripts/Publish-Editor.ps1
```

編輯器應用程式圖標由本頁 `branding/matrixtea.svg` 直接轉為多尺寸 ICO，重新產生方式為 `python scripts/Generate-Branding.py`。

## 建置與驗證

需要 .NET 8 SDK 或較新版本。以下命令不要求本機存在 RhythmClicker 或 Artelu。

```sh
dotnet build src/MatrixTea.Engine.MonoGame -c Release
dotnet build src/MatrixTea.Packaging.Cli -c Release
dotnet run --project tests/MatrixTea.Engine.Tests -c Release
dotnet run --project examples/MatrixTea.Headless -c Release
```

使用 MatrixTeaEngine.slnx 建置整個 solution 需要支援 SLNX 的 SDK（.NET 9.0.200 以上）；個別專案命令相容 .NET 8 SDK。具有桌面與 OpenGL 裝置的環境可執行：

```sh
dotnet run --project tests/MatrixTea.Engine.HostSmoke -c Release
```

GitHub Actions 在 Windows／Linux 建置各模組、編譯圖形探針並執行非圖形回歸。圖形探針的實際執行需要可用桌面。

## 執行模型

- 預設 120 Hz 固定更新、每幀最多 4 步、輸入時間上限 250 ms。整數 tick 計算避免不同執行階段的毫秒取整差異。
- 超出預算的整步時間會丟棄，分數餘量保留給插值；診斷提供丟棄時間、最近 120 幀平均及場景 CPU 回呼時間。
- 場景轉換在回呼結束後提交。穩態更新／渲染調度不配置管理物件；清理失敗仍繼續退出其他場景與引擎擁有的服務。
- MonoGame host 使用可變更新，由引擎管理固定步進；渲染介面在內容載入後重用。
- 節奏查找使用欄位索引；可變 Notes 清單保留相容掃描路徑。
- AtomicFile 提供文字、串流及非同步原子替換，保留前一版本備份。
- ActionInputBuffer 以固定容量保留帶時間戳的按下邊緣，避免固定更新重複消耗與焦點恢復後的幽靈輸入。
- PlaybackTimeline 管理倒數、暫停、恢復與校正；Windows 音訊接入讀取輸出裝置位置，避免解碼預讀時間造成判定偏移。
- CollisionGrid 查詢鄰近格子並分段移動；QuestJournal 驗證前置任務圖、保留未知存檔識別碼。
- ProceduralScore 產生可重現的原創 PCM 曲譜；GlyphTextRenderer 按字元及字級快取，不隨分數或顏色建立整行紋理。

完整 API 約定見 [開發指南](docs/development.md) 與 [架構](docs/architecture.md)。可執行範例位於 [examples/MatrixTea.Headless](examples/MatrixTea.Headless)。

## ATR 封裝

```sh
dotnet run --project src/MatrixTea.Packaging.Cli -- pack ./game ./game.atr
dotnet run --project src/MatrixTea.Packaging.Cli -- unpack ./game.atr ./output
```

解封裝目錄必須為空且不含路徑連結。ATR1 按區塊選擇 Brotli／Deflate／原始資料，使用 AES-256-GCM 加密驗證，限制展開大小、檔案數量與路徑。大型緩衝區池化並在歸還前清除；格式相容於既有 ATR1。CLI 支援 Ctrl+C 取消。

格式金鑰隨引擎散布。ATR 不提供不可逆向的 DRM 或發布者簽章；信任邊界見 [ATR1 規格](docs/atr-format.md)。

## 文件

- [開發指南](docs/development.md)：接入方式、所有權、例外處理與驗證。
- [架構](docs/architecture.md)：模組依賴及時間模型。
- [效能報告](docs/performance.md)：可重現微型基準、資料規模及限制。
- [驗證紀錄](docs/validation.md)：本機回歸、整合範圍及持續整合。
- [RhythmClicker 接入](docs/rhythmclicker-adoption.md)：可變清單與索引遷移。
- [貢獻指南](CONTRIBUTING.md)：程式與文件變更要求。
- [平台模組指南](docs/platform-modules.md)：冒險、節奏、Windows 音訊與文字接入。
- [可視化編輯器](docs/editor.md)：地圖、物件、任務、譜面、專案格式、預覽與發布。
- [授權政策](docs/licensing.md)：AGPL-3.0-only、自有閉源作品及第三方元件。

著作權：MoriTeahouse（森之宿茶室）。公開版本授權：[AGPL-3.0-only](LICENSE)。第三方元件及閉源作品接入政策見 [授權說明](docs/licensing.md)。
