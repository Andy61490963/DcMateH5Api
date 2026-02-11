# DbExecutor 重構說明（FormLogic）

## 目的

本次重構針對 `FormLogic` 層調整資料存取模型：

1. 新流程提供 **async API**（`Task` / `Task<T>`）作為主路徑，避免過去以同步方法包裝 async 的阻塞風險。
2. 維持現有功能穩定，保留相容同步入口供既有呼叫端逐步遷移。
3. 統一透過 `IDbExecutor` 執行 Dapper 查詢，讓連線、交易、SQL logging 行為集中管理。

---

## 這次做了什麼

### 1) FormLogic 介面增加 async 契約

- `IFormDataService`
  - `GetRowsAsync`
  - `GetTotalCountAsync`
  - `LoadColumnTypesAsync`
- `IFormFieldConfigService`
  - `GetFormFieldConfigAsync`
  - `LoadFieldConfigDataAsync`
- `ISchemaService`
  - `GetFormFieldMasterAsync`
  - `GetPrimaryKeyColumnAsync`
  - `GetPrimaryKeyColumnsAsync`
  - `ResolvePkAsync`
  - `IsIdentityColumnAsync`
  - `GetTableNameByTableIdAsync`
- `IDropdownService`
  - `GetOptionTextMapAsync`

> 同時保留同步方法，作為過渡期間的相容入口，目標為全面非同步。

### 2) 對應實作改為真正 async

- `FormDataService`
- `FormFieldConfigService`
- `SchemaService`
- `DropdownService`

以上類別的 async 方法都直接 `await IDbExecutor`，不再用 `GetAwaiter().GetResult()`。

### 3) DbExecutor 補齊同步 API（相容需求）

`IDbExecutor` 與 `DbExecutor` 新增同步版 `Query/Execute/ExecuteScalar` 與 InTx 版本，讓舊呼叫端可以在不使用阻塞等待 async 的前提下維持行為。

---

## 為什麼這是較佳解法

- **可維護性**：async 與 sync 各自有清楚路徑，重構可以分階段推進，不需一次大爆改。
- **可觀測性**：所有查詢仍經過 `DbExecutor`，SQL log 與相關 metadata 不會分散。
- **穩定性**：保留相容層，避免一次改動造成大量 controller/service 契約破壞。

---

## 複雜度與效能

- 單次查詢複雜度主要仍受 SQL 決定，程式端多為 `O(n)`（資料列映射）。
- async 路徑可在高併發情境降低執行緒阻塞，提升 Web API 吞吐。
- 空間複雜度與原版本一致，主要為查詢結果物件集合 `O(n)`。

---

## 風險與防護

1. **雙軌 API（sync/async）長期共存風險**
   - 防護：以文件標註同步入口為 migration bridge，後續逐步移除。
2. **交易路徑行為差異風險**
   - 防護：同步與非同步 InTx API 都透過同一 `BuildCmd` 與 logging 流程。
3. **大規模改介面導致編譯影響**
   - 防護：先提供 backward-compatible 同步簽章，避免一次性破壞。

---

## 下一步建議（第二階段）

1. 將 `FormService` / `FormMasterDetailService` / `FormMultipleMappingService` 對 `FormLogic` 的呼叫改成 `await ...Async`。
2. Controller 端改為 async action 全鏈路。
3. 當呼叫端都遷移完成後，移除同步相容方法，收斂為單一 async 架構。
