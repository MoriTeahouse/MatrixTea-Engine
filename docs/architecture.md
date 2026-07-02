# Architecture

MatrixTea Engine 以「核心引擎 + host 適配層 + 遊戲專案」三層結構組成。

## 層級

### Core

核心層只負責純邏輯，不碰 MonoGame 的圖形物件或任何平台 API。

包含：

- `EngineApplication` - 固定步進與場景調度。
- `EngineContext` - 共享執行期狀態。
- `SceneStack` - 場景堆疊。
- `RhythmJudgementEngine` - 判定邏輯。
- `RhythmSessionState` - 分數、combo、命中率。
- `RhythmReplay` - 回放資料。

### MonoGame host

MonoGame 層負責把 `Game`、`GraphicsDevice`、`SpriteBatch`、輸入與時間傳遞到核心。

### Game integration

RhythmClicker 這一層保留歌曲、UI、存檔、統計與外部服務，只讓核心處理高頻且穩定的玩法邏輯。

## Low-latency 路徑

1. host 擷取高精度 frame delta。
2. `FramePacer` 累積時間並吐出固定更新步數。
3. `RhythmJudgementEngine` 判斷命中結果。
4. `RhythmSessionState` 更新分數與 combo。
5. `RhythmReplay` 寫入單一事件流。

## 維護原則

- 一個模組只處理一件事。
- 資料模型盡量保持純資料物件。
- host 和 core 只透過介面連接。
- 遊戲專案可保留大量內容，但不要把所有服務揉在單一 `Game1` 裡。
