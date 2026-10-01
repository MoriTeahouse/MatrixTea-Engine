# 專案總覽

MatrixTea Engine 提供可獨立接入的遊戲執行模組，適用於節奏遊戲及需要固定步進、場景管理的 2D 遊戲。使用介面為 .NET 類別庫與封裝 CLI。

Core 處理計時、場景、服務、節奏及檔案寫入；MonoGame adapter 提供桌面接入；Packaging 提供 ATR1。遊戲保留自身內容與平台服務。Core／host 提供 net6.0、net8.0，封裝及工具使用 net8.0。

現有接入包含 RhythmClicker 的節奏判定及 Artelu 的場景／圖形 host。遊戲來源不包含於乾淨的引擎 clone，也不是引擎建置的必要依賴。獨立範例與測試位於 examples、tests。

公開介面見 [README](../README.md)、[開發指南](development.md)；模組關係見 [架構](architecture.md)；量測見 [效能報告](performance.md)。
