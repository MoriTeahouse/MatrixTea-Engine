# Contributing to MatrixTea Engine

感謝你想一起改善 MatrixTea Engine。

## 開發原則

- 以核心穩定性優先，任何新增功能都要先考慮是否會影響固定步進、判定準確度或回放一致性。
- 盡量維持 core / host / game 三層分工，不要讓遊戲專屬邏輯回流到核心之外的共用模組。
- 變更應該有明確的驗證方式，至少要能用 `dotnet build` 或對應子專案完成檢查。

## 提交流程

- 先在本機確認建置通過。
- 若變更影響玩家行為，請附上簡短說明與驗證步驟。
- 文件更新請與程式變更同步處理。

## 不應提交的內容

- 帳號或憑證資料。
- 統計資料庫、回放檔、發布輸出、暫存檔。
- `bin`、`obj`、或其他生成內容。

## 建議檢查

- `dotnet build src/MatrixTea.Engine.Core/MatrixTea.Engine.Core.csproj`
- `dotnet build src/MatrixTea.Engine.MonoGame/MatrixTea.Engine.MonoGame.csproj`
- `dotnet build RhythmClicker/ClickerGame.csproj`