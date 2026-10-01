# RhythmClicker 接入

RhythmClicker 透過資料轉換及 RhythmPlaySession 使用判定、計分與 miss 蒐集。來源位於 [RhythmClicker 倉庫](https://github.com/MoriTeahouse/RhythmClicker)，不包含於引擎的乾淨 clone。

| 遊戲資料／流程 | 引擎介面 |
|---|---|
| Beatmap / Note / break | RhythmBeatmap / BeatmapNote / BeatmapBreakPeriod |
| 判定窗與分數 | RhythmJudgementProfile / RhythmScoringProfile |
| 命中與逾期音符 | RhythmPlaySession.TryHit / CollectMisses |
| ReplayData | RhythmReplay 資料模型 |

歌曲、編輯器、音訊、UI、雲端同步及 replay 記錄／播放仍由遊戲管理。Game1 不必繼承 MatrixTeaGame 才能使用 Core 節奏模組。

既有整合將 session.Notes 指派給 LinkedList，並在其他流程中直接修改。Notes getter 因此切換為掃描模式，避免外部修改使索引失效。可指定 enableIndexedLookup:false，免除未使用索引的建構成本。

索引接入改用 RemainingNotes 渲染，所有命中／miss 移除交給 session，時間與欄位保持不變；編輯後建立新的 session。查找成本取決於判定窗附近音符，不掃描全部剩餘音符。批次 miss 在索引模式按時間排序，在相容模式按清單順序；非排序譜面需驗證事件順序。等距候選保留來源序列優先順序。

外部遊戲的建置可將 ProjectReference 指向本地引擎；標準 CI 不依賴外部遊戲。共用資料與判定回歸位於 tests/MatrixTea.Engine.Tests。
