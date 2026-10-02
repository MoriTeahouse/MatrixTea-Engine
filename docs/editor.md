# MatrixTea Studio 可視化編輯器

版本：0.1.0-test.1。MoriTeahouse（森之宿茶室）開發，原創程式與附帶範例採 AGPL-3.0-only。Windows x64 發布包自帶 .NET 8；解壓縮後執行 `MatrixTeaEditor.exe`。圖標直接由 README 的 `branding/matrixtea.svg` 轉換，包含 16／24／32／48／64／128／256 像素 ICO。

## 操作

1. 新專案包含茶室庭園、兩個前置任務及 32 音符範例。可開啟隨附 `Samples/TeaGarden.mtproject`。
2. 左側選擇地形後，在地圖上拖曳繪製。每次筆畫是一個復原交易；出生點與物件所在格不接受阻擋地形。
3. 選取物件，在檢查器修改名稱、格座標、對話或完成任務 ID；按「套用物件」。出生點可移動，不能刪除。
4. 任務分頁可建立任務、編輯 ID／標題／逗號分隔的前置 ID。循環、缺少前置及無效物件參照會拒絕套用。刪除任務前需移除其使用處。修改 ID 會同步更新參照。
5. 四軌譜面以 D／F／J／K 對應四欄。左鍵切換音符，右鍵移除，位置吸附至 1/16 拍；下方滑桿切換 8 秒視窗。BPM、曲長與判定偏移在檢查器套用。先儲存專案，再匯入 WAV／MP3；音樂複製至專案的 `assets/`。
6. 庭園預覽以 WASD 移動、Shift 跑步、E 互動；譜面預覽倒數 2 秒後開始，以裝置回報播放位置判定。無音樂時使用單調時鐘。Esc 或切換視窗停止預覽，預覽狀態不改寫專案。
7. Ctrl+S 儲存；地圖／譜面使用 Ctrl+Z／Ctrl+Y 復原與重做。文字輸入框保留自己的文字復原。儲存保留上一份 `.bak`，關閉有修改的專案時提供儲存選項。
8. ATR 匯出產生內容封裝，包含當前專案與被參照的音樂。**此功能不編譯獨立遊戲 EXE**；遊戲 host 可使用 Editor.Core 讀取資料。

## 資料與引擎接入

`.mtproject` 是 schemaVersion=1 的 UTF-8 JSON，包含場景地形、物件、任務及譜面。場景為 4–128 格、最多 32 個；每格 32 世界單位。草地、石徑、花草可走；石牆與水面阻擋。每場景必須有且只有一個出生點。物件種類表示內容分類；首版預覽提供文字互動與任務完成，入口切換場景及個別物件音效由遊戲 host 實作。

譜面限制 4 軌／20,000 音符／600 秒，偏移為 ±1,000 毫秒；同欄同時間不允許重複。曲長由開發者設定，音訊檔結束也會結束有音樂的預覽。資源使用相對 `assets/` 路徑，不接受路徑越界或連結。專案檔讀取上限 8 MB，歷史保留最多 64 次交易；非法交易不改變內容。首版沒有多人協作、動畫編輯、通用腳本 IDE 或自動遊戲編譯器。

```csharp
using MatrixTea.Editor.Core;
var project = ProjectDocument.Open("TeaGarden.mtproject").Model;
var collision = project.Scenes[0].CreateCollision();
var quests = project.CreateJournal();
var rhythm = project.CreateRhythmSession();
// host 提供輸入、畫面、音訊裝置與存檔；碰撞、任務、判定共用 Engine.Core。
```

ATR 內容可由 `MatrixTea.Packaging.Cli unpack` 解封裝，入口檔為 `project.mtproject`。ATR 匯出只收集參照資源，不包含專案資料夾中的其他檔案。

## 建置、發布與驗證

```powershell
dotnet build src/MatrixTea.Editor -c Release -warnaserror
dotnet run --project tests/MatrixTea.Editor.Tests -c Release
dotnet run --project src/MatrixTea.Editor -c Release -- --smoke --output ./artifacts/editor-smoke
pwsh -File scripts/Publish-Editor.ps1
python scripts/Generate-Branding.py
```

編輯器與圖標轉換工具需要 Windows；Editor.Core 與資料測試可跨平台執行。圖標工具只接受此專案圖標使用的 SVG 幾何元素，遇到不支援的元素會拒絕，避免默默遺失圖形。生成器不修改 SVG。

桌面探針驗證視窗、地圖碰撞、音符判定、實際裝置音訊、非法檢查器輸入、復原／重做、存讀與 ATR 匯出；截圖及 JSON 診斷寫入指定輸出資料夾。探針只使用自建範例專案，不開啟既有使用者專案。發布腳本拒絕非空輸出目錄，包含授權、第三方條文、實際依賴版本與相應來源修訂。
