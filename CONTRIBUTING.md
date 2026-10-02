# 貢獻指南

變更以共用模組穩定性、可重現驗證及 API 相容性為基本要求。

Core 保持不依賴圖形框架；MonoGame adapter 負責 host；Packaging 負責 ATR。遊戲內容、帳號、雲端與 UI 保留於遊戲專案。

```sh
dotnet build src/MatrixTea.Engine.MonoGame -c Release -warnaserror
dotnet build src/MatrixTea.Packaging.Cli -c Release -warnaserror
dotnet run --project tests/MatrixTea.Engine.Tests -c Release
dotnet run --project examples/MatrixTea.Headless -c Release
```

圖形變更在可用桌面執行 HostSmoke。性能變更提供資料規模、Benchmark 及等價行為測試；生命週期變更提供初始化／清理失敗測試。外部遊戲測試不能取代引擎的獨立回歸。

提交描述問題、最終行為與驗證結果。公開 API、預設值及相容性變更同步更新文件；文件以開發者為讀者，使用中性技術敘述。程式風格依 .editorconfig。

不提交 bin、obj、artifacts、發布封裝、帳號、憑證、存檔、回放及私人資料。公開程式採 AGPL-3.0-only，保留 MoriTeahouse（森之宿茶室）著作權及適用的第三方聲明。第三方貢獻依公開授權接收，不自動納入自有作品的內部閉源授權。
