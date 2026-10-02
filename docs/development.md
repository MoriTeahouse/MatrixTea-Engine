# 開發指南

本文件定義 MatrixTea Engine 的建置、接入方式、生命週期與驗證約定。共用模組不包含遊戲特定內容。

## 可視化內容製作

MatrixTea Studio 位於 `src/MatrixTea.Editor`，使用 Windows WPF；平台獨立的 `MatrixTea.Editor.Core` 負責 schema=1 專案驗證、交易歷史、原子儲存及 ATR 內容匯出。地圖預覽使用 CollisionGrid／QuestJournal，譜面預覽使用 RhythmPlaySession 與實際裝置位置。遊戲 host 可直接載入 `.mtproject` 並建立共用引擎狀態；專案不包含引擎程式碼或編譯後的遊戲。

操作、資料限制、資源搬移及建置範例見 [編輯器指南](editor.md)。完整 solution 現在包含 Windows 編輯器與 SVG 圖標工具；Linux 使用下方個別模組命令及 Editor.Tests 驗證，不建置 WPF 專案。應用程式圖標以 README SVG 為唯一來源，工具輸出 PNG／BMP／七尺寸 ICO，不另行重繪。

## 依賴

.NET 8 SDK 可建置個別專案；SLNX solution 需要 .NET 9.0.200 以上 SDK。Core／MonoGame 同時輸出 net6.0、net8.0；Packaging／CLI 使用 net8.0。新遊戲可使用 net8.0，既有 net6.0 遊戲仍有相容目標。測試及範例在 .NET 8 執行，不要求 .NET 6 Runtime。

```xml
<ItemGroup>
  <ProjectReference Include="../MatrixTea-Engine/src/MatrixTea.Engine.Core/MatrixTea.Engine.Core.csproj" />
  <ProjectReference Include="../MatrixTea-Engine/src/MatrixTea.Engine.MonoGame/MatrixTea.Engine.MonoGame.csproj" />
</ItemGroup>
```

Core 不依賴圖形套件。MonoGame 使用 DesktopGL，部署平台仍須有相容圖形驅動。

Windows 的串流音訊與字形接入可額外參考 MatrixTea.Engine.Desktop（net8.0-windows），接入範例及所有權見 [平台模組指南](platform-modules.md)。跨平台 host 可保留 Core／MonoGame 並自行提供相應服務。公開引擎與以其 AGPL 授權形成的結合作品適用 AGPL-3.0-only；授權政策見 [授權說明](licensing.md)。

## 自訂 host

Pump(delta) 只回傳可執行的固定步數；host 按回傳數呼叫 Update，再於實際渲染時呼叫 Render。

```csharp
using var engine = new EngineApplication(new EngineOptions
{
    Title = "Example",
    TargetUpdateStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 120),
    MaxUpdatesPerFrame = 4,
    MaxFrameDelta = TimeSpan.FromMilliseconds(250)
});

int updates = engine.Pump(frameDelta);
for (int i = 0; i < updates; i++)
    engine.Update(engine.Context.Options.TargetUpdateStep);

engine.Render(renderSurface);
```

片段中的 frameDelta、renderSurface 及場景由 host 提供。可執行非圖形範例見 [Headless](../examples/MatrixTea.Headless/Program.cs)。

Interpolation 為剩餘固定步比例。Consume 後小於 1；單獨 Add 尚未 Consume 時可能為 1。負數 delta 不推進模擬。超出時間上限及更新預算的整步時間會丟棄，並記錄於 DroppedSimulationTime；此策略限制追趕工作，不保證執行停頓期間的每個模擬步。

## MonoGame 接入

遊戲繼承 MatrixTeaGame，於 OnLoadContent 建立場景。OnBeforeFixedUpdates 在每個 host 幀擷取一次輸入；OnFixedUpdate 可處理場景之外的固定更新。

IsFixedTimeStep 預設 false，固定步進由引擎管理。SynchronizeWithVerticalRetrace 預設 true，由 EngineOptions 設定。

UseHighResolutionTiming=true 使用單調時鐘 Capture 的真實 delta；false 使用 GameTime.ElapsedGameTime。自訂 IEngineClock 應提供正確 TickFrequency。歌曲時間應使用音訊播放位置或同步的單調絕對時間，不從固定更新次數推算。

PauseWhenInactive=true 在失去焦點時暫停模擬，恢復時清空追趕餘量，不重設絕對時鐘原點。Draw 仍由 host 管理。共用 render surface 與 SpriteBatch 的有效期為該次內容載入。

## 場景生命週期

SceneStack 首次操作時綁定執行緒。轉換、Update、Render、Shutdown 由同一執行緒呼叫；背景工作應將結果交回 host 執行緒。此類別不是跨執行緒命令佇列。

| 操作 | 行為 |
|---|---|
| Push | Enter 新場景後加入堆疊 |
| Pop | 移除最上層並呼叫 Exit |
| Replace | 清空整個堆疊、退出舊場景，再 Enter 新場景 |
| Clear | 退出全部場景；堆疊可重用 |
| Shutdown | 清空並永久關閉堆疊 |

僅最上層接受 Update／Render。重複加入活動場景實例會拒絕執行。回呼中的轉換在回呼完成後提交；佇列及每次提交上限均為 256。

Enter 失敗時會呼叫 Exit 清理部分初始化，Exit 必須能處理未完全建立的資源。Clear／Shutdown 即使 Exit 失敗仍繼續清理其餘場景，最後聚合回報例外。Replace 不會在新 Enter 失敗後恢復已退出的舊場景；host 應處理空堆疊或結束流程。

Update／Render 不支援在自身回呼內重入。EngineApplication.Dispose 必須在場景回呼外執行。Shutdown 期間拒絕從 Exit 再加入場景。

## 服務所有權

AddSingleton 為借用註冊，由呼叫端管理。AddOwnedSingleton 僅接受 IDisposable，將物件生命週期交給引擎。

```csharp
engine.Context.Services.AddSingleton<IInputSource>(input);
engine.Context.Services.AddOwnedSingleton(audioService);
var service = engine.Context.Services.GetRequired<IInputSource>();
```

IInputSource、input、audioService 代表遊戲自己的介面及實例。擁有的服務以反向註冊順序釋放；同一實例的多種型別註冊只釋放一次。替換或 Remove 不提前釋放已交付所有權的物件，它們保留至引擎 Dispose。

Dispose 先退出場景，再釋放擁有的服務。個別失敗不會中斷其他清理，重複 Dispose 不會重複釋放。借用服務不受影響。ServiceRegistry 不保證並行存取，由 host 協調使用。

## 節奏索引

RhythmPlaySession 建構時快照音符時間／欄位，建立欄位有序陣列與未處理音符時間索引。TryHit 二分查找候選窗，選最近音符；相同距離保留來源序列優先順序。已移除節點略過，索引保留至 session 結束。

```csharp
var session = new RhythmPlaySession<BeatmapNote>(
    beatmap.Notes, note => note.Time, note => note.Column);
var hit = session.TryHit(songTimeSeconds, column);
foreach (var miss in session.CollectMisses(songTimeSeconds)) { /* gameplay */ }
foreach (var note in session.RemainingNotes) { /* render */ }
```

只讀渲染使用 RemainingNotes。索引模式不得改寫 TNote 的時間／欄位；編輯後建立新 session。存取可變 Notes 會永久切換為掃描模式，維持舊整合直接修改 LinkedList 的語義。已知需要可變清單時可指定 enableIndexedLookup:false，避免未使用的索引成本。

索引模式的批次 miss 按時間／來源序號排序；掃描模式按清單順序。未排序譜面的事件順序可能不同。判定窗須滿足 0 ≤ Perfect ≤ Great ≤ Good，分數非負，時間為有限值。Miss 閾值不小於 Good 窗。分數溢位在狀態變更之前拒絕。無 miss 的輪詢不配置新清單。

JSON 資料欄位不變。音訊同步、輸入排程及 replay 播放器由遊戲實作。

## 原子檔案寫入

AtomicFile.WriteText 不配置整份 UTF-8 位元組副本。Write／WriteAsync 直接將序列化資料寫入暫存 stream，回呼不接管 stream 所有權。序列化例外或 async 取消會清除暫存檔且不替換原檔。

替換前 flush，前一版本保存在 .bak。同一目的檔的多個寫入由呼叫端序列化；API 不提供多程序交易、讀取恢復或目錄同步。網路檔案系統與硬體斷電耐受依平台而定。

## 診斷與驗證

EngineMetrics.Snapshot 提供 frame／update／render 次數、最近 120 幀平均、歷史最大 delta、最近場景 update／render 的 CPU 時間及丟棄模擬時間。此資料不是 GPU 時間或整個遊戲的完整 CPU profile。

```sh
dotnet run --project tests/MatrixTea.Engine.Tests -c Release
dotnet run --project tests/MatrixTea.Engine.HostSmoke -c Release
pwsh -File scripts/Validate.ps1 -Graphics
pwsh -File scripts/Benchmark.ps1 -IncludePackaging
```

一般 CI 只編譯圖形探針，實際執行需要桌面。Validate 的 -LegacyAtr 參數可追加既有 Artelu ATR1 相容讀取。封裝 Benchmark 需要 Git 歷史中的 9d56e36。結果位於忽略的 artifacts。

性能修改提供等價行為測試及可重現量測，不以微型基準推定遊戲 FPS。生命週期修改涵蓋部分初始化、回呼內轉換與退出失敗。ATR 變更維持讀取相容性或提升格式版本。文件以開發者為讀者，使用中性技術敘述。
