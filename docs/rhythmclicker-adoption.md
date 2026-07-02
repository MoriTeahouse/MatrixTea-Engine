# RhythmClicker Adoption

這份文件說明 MatrixTea Engine 如何接入 RhythmClicker。

## 已接上的部分

- RhythmClicker 的 beatmap JSON 已經可以轉成 MatrixTea 的共用節奏模型。
- MatrixTea Core 已能直接描述 note、break、判定與 replay 資料。

## 接下來應優先搬移的部分

1. 更新迴圈與節奏時鐘。
2. note 判定與 scoring。
3. replay 記錄與播放。
4. scene/state 管理。

## 建議的對應關係

- `Game1.Update` -> `EngineApplication.Pump` + `SceneStack.Update`。
- `Beatmap` -> `MatrixTea.Engine.Core.Rhythm.RhythmBeatmap`。
- `ReplayData` -> `MatrixTea.Engine.Core.Rhythm.RhythmReplay`。
- `GameConfig` 判定窗 -> `RhythmJudgementProfile` + `RhythmScoringProfile`。

## 最小整合原則

不要一次把整個 `Game1` 砍掉。先把資料模型與判定核心換成引擎版本，再把場景與 host 分層拆開。
