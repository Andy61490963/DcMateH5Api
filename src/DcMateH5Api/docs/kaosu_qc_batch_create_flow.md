# Kaosu QC 批次新增 API 流程說明

## 目標
提供 `KaosuQcController` 一支 POST API，支援一次新增多筆單頭（Header）及其多筆單身（Detail），並確保整包交易全成全敗。

## 路由
- `POST api/KaosuQc/KaosuQc/CreateBatch`

## 資料流與業務規則
1. Controller 收到 request 後呼叫 `IKaosuQcService.CreateBatchAsync`。
2. Service 先做記憶體層驗證：
   - `Headers` 不可為空。
   - 每筆 Header 的 `InspectionNo` 不可為空。
   - 同一 request 內不可有重複 `InspectionNo`。
3. 開啟 DB Transaction。
4. 先查 DB 內是否已存在任一 `InspectionNo`：
   - 若存在，拋錯並 rollback。
5. 逐筆 Header 寫入：
   - SID 由 `RandomHelper.GenerateRandomDecimal()` 產生。
   - 若 SID 碰撞，最多重試 10 次。
6. 逐筆 Detail 寫入：
   - `HEADER_SID` 使用對應 Header SID。
   - `Detail.InspectionNo` 一律覆蓋成 Header 的 `InspectionNo`。
7. 全部成功則 Commit，回傳成功新增的 `InspectionNo` 清單。
8. 任一步驟失敗則 Rollback，Controller 統一回 `500` + 安全錯誤訊息，並記錄完整 log。

## 為什麼這樣設計
- **一致性**：同一 transaction 保證主從資料不會部分成功。
- **效能**：先批次檢查 `InspectionNo` 是否存在，避免逐筆查 DB。
- **安全性**：全部 SQL 使用 Dapper 參數化；錯誤訊息對外不暴露內部細節。
- **可維護性**：
  - 路由集中在 `Routes`。
  - Request/Response 使用強型別模型。
  - SID/審計欄位邏輯集中在 Service 私有方法。

## 失敗案例與防呆
- 前端重複送同一 `InspectionNo`：服務層直接擋下。
- 資料庫已存在 `InspectionNo`：整包 rollback。
- SID 極端碰撞：重試後仍失敗則拋錯，避免主鍵衝突造成髒資料。
- 前端 Detail 傳錯 `InspectionNo`：寫入前強制覆蓋為 Header 值。
