# MatrixTea Engine

MatrixTea Engine 是模組化的 .NET 遊戲執行基座，提供固定步進、場景生命週期、服務管理、節奏判定、原子檔案寫入及 ATR 遊戲封裝。圖形接入層使用 MonoGame DesktopGL；核心與封裝模組可獨立使用。

## 模組

| 專案 | 目標框架 | 職責 |
|---|---|---|
| [MatrixTea.Engine.Core](src/MatrixTea.Engine.Core) | .NET 6 / .NET 8 | 計時、固定步進、場景、服務、診斷、節奏與通用檔案寫入 |
| [MatrixTea.Engine.MonoGame](src/MatrixTea.Engine.MonoGame) | .NET 6 / .NET 8 | MonoGame 視窗、圖形裝置與引擎循環接入 |
| [MatrixTea.Engine.Packaging](src/MatrixTea.Engine.Packaging) | .NET 8 | ATR1 讀寫、壓縮與 AES-GCM 驗證 |
| [MatrixTea.Packaging.Cli](src/MatrixTea.Packaging.Cli) | .NET 8 | 命令列封裝工具 |

Core 不依賴 MonoGame。遊戲內容、輸入映射、音訊排程、UI、物理及網路由遊戲或其他服務提供；此專案不包含視覺編輯器。

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

授權：[MIT](LICENSE)。
