# FORM_FIELD_DELETE_GUARD_SQL CRUD 流程說明

此文件說明 `FORM_FIELD_DELETE_GUARD_SQL` 刪除防呆規則的 CRUD 流程與主要設計重點，方便後續維護與擴充。

## 功能目的

`FORM_FIELD_DELETE_GUARD_SQL` 用於定義表單刪除時的防呆 SQL。當 SQL 回傳筆數 > 0 時，前端可視為「不可刪除」的規則依據。

## 刪除驗證 API（FormDeleteGuardController）

| 動作 | 方法 | 路由 | 說明 |
| --- | --- | --- | --- |
| 刪除前驗證 | POST | `/form/delete-guard/validate` | 依 Guard SQL 逐筆驗證是否可刪除 |

### 請求範例

```json
{
  "formFieldMasterId": "guid",
  "key": "EQP_NO",
  "value": "123"
}
```

### 回應範例

```json
{
  "success": true,
  "data": {
    "canDelete": false,
    "blockedByRule": "設備狀態限制"
  }
}
```

### 驗證流程重點

1. 依 `FORM_FIELD_MASTER_ID` 查詢規則（`IS_ENABLED = 1` 且 `IS_DELETE = 0`），並依 `RULE_ORDER` 排序。
2. 逐筆檢查 Guard SQL：
   - 必須以 `SELECT` 開頭。
   - 不可包含 `;`。
   - 禁止關鍵字：`INSERT / UPDATE / DELETE / DROP / ALTER / CREATE / EXEC / WAITFOR`。
   - 使用 Regex `@\w+` 擷取參數名稱並確認前端 `Key` 存在。
3. 以 Dapper 參數化執行 Guard SQL，取得 `CanDelete`。
4. 若 `CanDelete = false`，立即回傳該規則的 `NAME` 作為阻擋原因。
5. 全部規則通過則回傳 `CanDelete = true`。

## API 端點（FormDesignerController）

| 動作 | 方法 | 路由 | 說明 |
| --- | --- | --- | --- |
| 查詢清單 | GET | `/Form/FormDesigner/delete-guard-sqls?formFieldMasterId={id}` | 依主檔 ID 篩選（可不帶參數） |
| 查詢單筆 | GET | `/Form/FormDesigner/delete-guard-sqls/{id}` | 取得單筆規則 |
| 新增 | POST | `/Form/FormDesigner/delete-guard-sqls` | 建立一筆規則 |
| 更新 | PUT | `/Form/FormDesigner/delete-guard-sqls/{id}` | 更新指定規則 |
| 刪除 | DELETE | `/Form/FormDesigner/delete-guard-sqls/{id}` | 軟刪除指定規則 |

## 程式碼流程

1. **Controller 層**
   - `FormDesignerController` 接收請求，並取得目前登入使用者 ID（若未登入則為 `null`）。
   - 呼叫 `IFormDesignerService` 對應方法進行 CRUD。

2. **Service 層**
   - `FormDesignerService` 使用 `SQLGenerateHelper`（內部為 Dapper）進行 CRUD。
   - 讀取後在記憶體依 `RULE_ORDER`、`SEQNO` 排序，以避免直接硬編碼 SQL。
   - 更新與刪除前先查詢既有資料（避免更新不存在的資料）。
   - 刪除採用 **軟刪除**，透過 `IS_DELETE = 1` 保留歷史資料。

3. **SQL 實作**
   - 透過 `SQLGenerateHelper` 產生 SQL，避免手寫字串與魔法字串。
   - 讀取時一律加上 `IS_DELETE = 0` 避免讀到已刪除資料。
   - 新增/更新/刪除會先取回物件再更新欄位，保持狀態一致性。

## 錯誤處理

- 查詢不到資料時回傳 `404 NotFound`。
- 更新/刪除流程會先讀取資料，若不存在則不執行後續 SQL。

## 維護建議

- 若前端需要批次排序調整，可擴充 `RULE_ORDER` 更新 API。
- 若需審計需求，可在 SQL 中新增更完整的操作記錄或記錄表。
