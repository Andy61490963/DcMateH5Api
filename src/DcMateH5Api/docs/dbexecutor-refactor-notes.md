# DbExecutor 重構說明（Infrastructure / FormLogic）

## 目的

本次調整將 `FormLogic` 內部原本直接依賴 `SqlConnection` 的資料存取流程，統一改為透過 `IDbExecutor` 呼叫 Dapper 查詢，降低各 Service 對連線細節的耦合。

## 重構重點

1. **依賴反轉**
   - `FormDataService`
   - `FormFieldConfigService`
   - `DropdownService`
   - `SchemaService`

   以上服務改為注入 `IDbExecutor`，不再保存 `SqlConnection` 欄位。

2. **資料存取一致化**
   - 單查詢：改用 `QueryAsync<T>` / `QueryFirstOrDefaultAsync<T>` / `ExecuteScalarAsync<T>`
   - 交易中查詢：改用 `QueryInTxAsync<T>` / `QueryFirstOrDefaultInTxAsync<T>` / `ExecuteScalarInTxAsync<T>`
   - 多結果集：在 `FormFieldConfigService` 內透過 `TxAsync` 與 Dapper `QueryMultipleAsync` 完成單次 round-trip 取回。

3. **商業邏輯維持不變**
   - SQL 條件、排序、分頁、欄位轉型規則維持原本實作。
   - 原有錯誤訊息與輸入驗證邏輯保留。

## 效益

- **可維護性提升**：資料庫操作 API 統一，後續若要追加追蹤、逾時策略、重試策略，可集中於 `DbExecutor`。
- **可測試性提升**：Service 可直接 mock `IDbExecutor`。
- **可觀測性提升**：沿用 `DbExecutor` 既有 SQL logging 能力。

## 風險與防護

- 目前部分 Service 仍保留同步公開方法，內部以 `GetAwaiter().GetResult()` 等待 async；需避免在 UI 同步內容環境使用以降低 deadlock 風險。
- 建議下一步逐步把 Abstractions 與 Controller 一併改為 `async/await` 端到端流程，完全移除同步阻塞呼叫。
