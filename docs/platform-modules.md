# 冒險、節奏與桌面接入

本次擴充以共用 Core 為基礎。冒險與音遊可組合相同的計時、輸入及檔案服務；遊戲特定的地圖、曲庫、敘事與介面仍屬遊戲層。

## 輸入

ActionInputBuffer 在 host 幀擷取邊緣，固定更新以 TryReadPress 消耗。持續按住不會再次入列；一次按下只能消耗一次。TimestampSeconds 必須是有限且不倒退的同一個單調時鐘。容量用盡時丟棄最舊事件並累計 DroppedPresses。

失去焦點、重新綁鍵或恢復遊戲時，Synchronize 以實際按住狀態清空待處理事件，避免未釋放按鍵被視為新按下。穩態 Capture 及消耗不配置管理物件。

## 節奏時間與校正

PlaybackTimeline 支援負數倒數、Pause、Resume、Seek 及播放速率。Position 在正常播放時不倒退；只有明確 Start／Seek 可改變時間原點。ObserveAudio 的有限修正不能取代真實輸出裝置位置。

Windows DeviceAudioPlayer 使用 NAudio WaveOutEvent 的裝置位元組位置，音量為 0–1。解碼器預讀位置不等於玩家已聽到的位置。遊戲應以同一歌曲時間處理命中與逾期音符，先處理輸入，完成最後一批 miss 後再結算；提早失敗仍應計入所有剩餘音符。

LatencyCalibration 跳過兩次暖身，拒絕超過 350 ms 的離群值，以最多 32 筆樣本中位數產生 ±500 ms 的輸入修正。至少八筆有效樣本可提交。若玩家平均晚 40 ms，建議修正為 -40 ms；視覺偏移不應改變判定。幀排程的節拍與人工敲擊校正包含反應及裝置誤差，不是音訊取樣級延遲量測。

## 冒險

CollisionGrid 的座標單位由 TileSize 定義，查詢附近格子而非掃描整張地圖。Move 分段檢查並按軸滑動，限制單次位移及地圖尺寸，降低跨越薄牆的風險。地圖外視為阻擋；移動前應使用 CanStand 檢查存檔出生點。

QuestJournal 要求唯一識別碼、有效前置參照及無循環圖。Complete 具冪等性；ExportCompleted 保留未被當前內容識別的已完成識別碼，以便內容版本間遷移。遊戲仍需定義自己的存檔 schema 與版本拒絕策略。

## 原創曲譜與文字

ProceduralScore 輸出 22,050 Hz 單聲道 PCM16 WAV，提供固定音階、和弦、節拍及淡入淡出。同一組參數產生相同內容。輸出使用 AtomicFile，適合初次啟動產生示範曲庫；不應在 Draw 或每幀 Update 內生成完整歌曲。

GlyphTextRenderer 是 Windows 選用實作，使用作業系統中文字型，按字元及字級快取紋理。動態分數與顏色重用相同字形。字形建立、繪製與 Dispose 必須在圖形裝置所屬執行緒；快取在 Dispose 清空，不承諾任意 Unicode／字級組合的固定記憶體上限。遊戲應限制使用字級與使用者輸入長度。

## 散布

Windows 遊戲可使用 dotnet publish --self-contained true -r win-x64 將 .NET 執行環境與 native 依賴隨包散布；仍需要可用的作業系統圖形／音訊裝置及驅動。使用者資料目錄應由啟動器或 --data-root 明確指定，更新包先驗證並準備於獨立目錄。公開開源遊戲須隨包提供授權與對應原始碼取得方式。
