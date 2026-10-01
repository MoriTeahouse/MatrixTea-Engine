# 架構

MatrixTea Engine 分為 Core、MonoGame adapter、Packaging 與遊戲接入層。

```text
Game / custom host
 ├── Core
 ├── MonoGame adapter ── Core + MonoGame DesktopGL
 └── Packaging
CLI ── Packaging
Tests / benchmarks ── 對應模組
```

Core 不持有圖形框架型別；Packaging 不依賴遊戲內容或 GPU。遊戲負責具體場景、UI、輸入、音訊與儲存格式。

EngineApplication 擁有 FramePacer、SceneStack、EngineContext。FramePacer 以整數 tick 計算更新數及分數餘量，丟棄超額工作。Context 提供設定、單調時鐘、服務與度量；host 驅動 Pump／Update／Render。

SceneStack 在單一執行緒使用值型別轉換佇列，回呼結束後提交。Clear 可重用，Shutdown 永久關閉。服務區分借用與引擎所有權，場景先退出再釋放擁有的服務。

RhythmPlaySession 使用欄位有序陣列二分查找候選窗，全域時間索引追蹤逾期音符。可變 LinkedList API 使用掃描模式維持修改語義。Replay 只提供資料模型，音訊同步及播放器由遊戲負責。

AtomicFile 使用同目錄暫存檔、flush 與替換，資料格式及備份讀取策略由遊戲提供。ATR1 使用有界區塊與池化緩衝；歸還前清除資料。解封裝中途失敗可能留下部分檔案，安裝器應使用 staging 並負責清理。格式金鑰不是私密發布者簽章。

Core／封裝回歸不依賴 GPU；HostSmoke 實際建立 MonoGame／OpenGL 裝置。Benchmark 量測特定路徑，不代表完整遊戲效能。生命週期約定見 [開發指南](development.md)。
