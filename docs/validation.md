# 驗證紀錄

## 本機驗證（2026-10-01）

Windows x64，.NET SDK 10.0.103；.NET 8 測試執行階段 8.0.24。

| 範圍 | 結果 |
|---|---|
| SLNX 全專案 Release 建置，警告視為錯誤 | 通過，0 警告／0 錯誤 |
| Core／封裝自帶回歸 | 66,082 項斷言通過 |
| 已發布 Artelu test.2 ATR1 相容讀取 | 通過；加入此檢查後共 66,083 項 |
| MonoGame／OpenGL HostSmoke | 固定更新、共用 surface、渲染中延後轉換、GPU 資源清理通過 |
| 非圖形範例 | 60 個 host 幀、120 個固定更新 |
| Artelu 接入回歸 | 2,486 項通過 |
| Artelu 實際圖形流程 | 19 階段通過 |
| 本機 RhythmClicker net6.0 接入建置 | 通過；26 項既有警告，0 錯誤 |
| README／文件本機連結及 diff 空白檢查 | 通過 |

Core 回歸包含固定步進時間守恆與溢位、原有建構子簽章、延後場景轉換、退出失敗後繼續清理、部分 Enter 失敗回滾、執行緒限制、轉換循環上限、服務所有權、計分溢位、30 組節奏判定資料、可變清單、空 miss 配置、同步／非同步原子替換、取消、ATR 跨區塊還原、篡改與截斷拒絕。

## 持續整合

Engine validation 工作流在 Windows／Ubuntu 執行建置、非圖形回歸、基準專案建置及非圖形範例。圖形探針只編譯，實際桌面執行由本機驗證。每次提交的實際結果可於 [GitHub Actions](https://github.com/MoriTeahouse/MatrixTea-Engine/actions/workflows/engine.yml) 查看。

本機來源可使用 scripts/Validate.ps1；既有遊戲的整合測試透過 ProjectReference 或 EngineRoot 指向引擎來源。外部遊戲不是標準建置依賴。

## 限制

圖形測試未涵蓋其他 GPU、其他桌面平台、長時間裝置重設或低階硬體。節奏索引基準不包括建構成本；可變 Notes 整合使用相容路徑。微型基準及管理配置不能推定完整遊戲 FPS，量測方法見 [效能報告](performance.md)。
