# Development Guide

## 建置

```bash
dotnet build src/MatrixTea.Engine.Core/MatrixTea.Engine.Core.csproj
dotnet build src/MatrixTea.Engine.MonoGame/MatrixTea.Engine.MonoGame.csproj
dotnet build RhythmClicker/ClickerGame.csproj
```

## 建議工作流

1. 先改核心層，再改 host 或遊戲專案。
2. 對 rhythm 相關變更，優先維持 beatmap JSON 向後相容。
3. 新增服務時先放進 `ServiceRegistry` 或獨立介面，不要直接散落在 `Game1`。
4. 做大改動前先在 `docs/rhythmclicker-adoption.md` 補齊接線說明。

## 程式風格

- 偏好小型、可命名清楚的型別。
- 避免把判定、IO、UI、音訊與回放寫在同一個類別。
- 保留 nullable，並維持現有專案的明確命名方式。

## 驗證順序

1. `dotnet build`。
2. 針對 beatmap 或 replay 變更做最小回歸測試。
3. 再做遊戲專案的整合驗證。
