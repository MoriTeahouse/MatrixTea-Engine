# MatrixTea Engine

MatrixTea Engine 是為 RhythmClicker 打造的專屬遊戲引擎與運行基座。
它保留足夠的抽象層，讓核心可以維持低延遲、高可維護性，同時也能延伸到其他節奏或 2D 遊戲。

## 亮點

- 低延遲、固定步進的核心迴圈。
- 清楚分離的場景、節奏、回放、設定與服務層。
- 可直接對接 RhythmClicker 的 beatmap 與 replay 資料。
- MonoGame host 基底，方便掛載到實際遊戲專案。

## 專案結構

- [src/MatrixTea.Engine.Core](src/MatrixTea.Engine.Core) - 核心運行時、節奏 session、判定、回放與服務註冊。
- [src/MatrixTea.Engine.MonoGame](src/MatrixTea.Engine.MonoGame) - MonoGame host 基底。
- [RhythmClicker](https://github.com/MoriTeahouse/RhythmClicker) - 已接入 MatrixTea 的 RhythmClicker 原始碼。
- [docs/overview.md](docs/overview.md) - 專案總覽。
- [docs/architecture.md](docs/architecture.md) - 架構與模組設計。
- [docs/development.md](docs/development.md) - 開發與維護指南。
- [docs/rhythmclicker-adoption.md](docs/rhythmclicker-adoption.md) - RhythmClicker 採用說明。

## 建置

```bash
dotnet build src/MatrixTea.Engine.Core/MatrixTea.Engine.Core.csproj
dotnet build src/MatrixTea.Engine.MonoGame/MatrixTea.Engine.MonoGame.csproj
dotnet build RhythmClicker/ClickerGame.csproj
```

## 目前狀態

MatrixTea Core 與 MonoGame host 已可建置，RhythmClicker 也已接上共用 beatmap、判定與 replay 的橋接層。
接下來可以逐步把更多遊戲流程從 `Game1` 移入引擎核心。

## Contributing

歡迎針對核心迴圈、節奏判定、回放系統、文件與相容性提出修改。

- 請先確認 `dotnet build src/MatrixTea.Engine.Core/MatrixTea.Engine.Core.csproj` 與 `dotnet build src/MatrixTea.Engine.MonoGame/MatrixTea.Engine.MonoGame.csproj` 可通過。
- 變更請保持小而集中，避免把引擎核心與遊戲範例混在同一個 PR 裡。
- 若要擴充 RhythmClicker 的接入層，優先透過 `MatrixTeaIntegration.cs` 或核心服務介面完成。
- 請勿提交帳號資料、統計資料庫、回放輸出、發布產物或其他機密/生成檔案。

完整規範請見 [CONTRIBUTING.md](CONTRIBUTING.md)。

## ATR 遊戲封裝

新增獨立 .NET 8 封裝模組與 CLI，使用適應式 Brotli／Deflate、AES-256-GCM 和 HMAC-SHA-256。Core／MonoGame 的 .NET 6 相容性維持不變。格式、工具與防逆向限制見 [ATR1 文件](docs/atr-format.md)。

## License

MatrixTea Engine 採用 MIT License，細節請見 [LICENSE](LICENSE)。
