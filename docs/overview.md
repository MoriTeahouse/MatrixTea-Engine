# Overview

MatrixTea Engine 的設計重點不是做成單一龐大的遊戲框架，而是把 RhythmClicker 真正需要的高頻路徑先穩定下來，其他服務再以介面和適配層擴充。

## 設計目標

- 低延遲：節奏判定與更新循環使用固定步進與高精度時鐘。
- 可維護：把場景、資料、服務與 host 分開。
- 可擴充：核心不直接依賴 MonoGame 的具體實作。
- 可移植：節奏模型與回放模型可跨專案共享。

## 核心模組

- Runtime：`EngineApplication`、`EngineContext`、`SceneStack`、`FramePacer`。
- Rhythm：`RhythmBeatmap`、`RhythmJudgementEngine`、`RhythmSessionState`、`RhythmReplay`。
- Services：`ServiceRegistry` 與 host 注入的外部服務。
- Host：MonoGame 適配層提供視窗、圖形與輸入接線。

## 與 RhythmClicker 的關係

RhythmClicker 目前已採用共用 beatmap 資料模型，後續的下一步是把如下內容搬進 MatrixTea：

- note 命中判定。
- 場景轉換。
- replay 記錄與播放。
- 遊戲內低延遲時鐘與更新節奏。
