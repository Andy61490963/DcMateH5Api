# Form 服務非同步化重構說明

## 目標

本次第二階段重構，將以下三個核心服務與對應 API Controller **全面改為 Task 簽章**：

- `FormService`
- `FormMasterDetailService`
- `FormMultipleMappingService`

同時移除這三層對外同步 bridge，統一由 Controller → Service 以 `async/await` 串接。

---

## 重構內容

### 1. 介面契約全面改為非同步

- `IFormService`
  - `GetFormListAsync`
  - `GetFormSubmissionAsync`
  - `SubmitFormAsync`（含交易版）
  - `GetFieldTemplatesAsync`
- `IFormMasterDetailService`
  - `GetFormListAsync`
  - `GetFormSubmissionAsync`
  - `SubmitFormAsync`
- `IFormMultipleMappingService`
  - `GetFormMastersAsync`
  - `GetFormsAsync`
  - `GetMappingListAsync`
  - `AddMappingsAsync`
  - `RemoveMappingsAsync`
  - `ReorderMappingSequenceAsync`
  - `UpdateMappingTableDataAsync`

### 2. Controller 全面 await 服務層

- `FormController`
- `FormMasterDetailController`
- `FormMultipleMappingController`

以上 Action 改為 `async Task<IActionResult>`，並將原本同步呼叫改為 `await` 非同步服務。

### 3. 交易流程改用非同步交易封裝

涉及主明細與多對多批次操作的流程，改為使用 `WithTransactionAsync`，在交易內安全執行 async DB 存取，避免同步交易 + await 混用造成風險。

---

## 效能與複雜度

- **時間複雜度**：維持原本查詢邏輯，主要仍由 SQL 查詢成本主導。
- **空間複雜度**：維持 `O(n)`（資料列映射與組裝）。
- **可伸縮性**：在高併發 API 請求下，async 路徑可減少執行緒阻塞、提升吞吐。

---

## 風險與防護

1. **簽章變更衝擊呼叫端**
   - 防護：Controller 與對應服務同批調整，避免介面落差。
2. **交易內 await 造成流程不一致**
   - 防護：統一改用 `WithTransactionAsync`，由同一交易邊界包裹。
3. **混用同步/非同步 API**
   - 防護：本階段針對指定三個服務移除對外同步 bridge，避免新程式碼回頭走同步路徑。

---

## 後續建議

下一步可進一步把 FormLogic 仍保留的同步相容入口也移除（僅保留 `*Async`），讓整個 Form Domain 收斂為單一路徑的 async 架構。


## 本次修正（編譯錯誤）

- 修正 `FormMultipleMappingService` 中 `BuildDropdownMetaMap` 相關流程：
  - 將方法改為 `BuildDropdownMetaMapAsync`。
  - 將呼叫端（Linked/Unlinked 載入流程）改為 async/await。
  - 消除「await 僅可在 async 方法內使用」的編譯錯誤。


## 本次修正（移除 Infrastructure 直接注入 SqlConnection）

以下服務已改為注入 `IDbExecutor`，不再於建構式直接注入 `SqlConnection`：

- `DropdownSqlSyncService`
- `FormDesignerService`
- `FormMasterDetailService`
- `FormMultipleMappingService`
- `FormOrphanCleanupService`
- `FormService`

### 調整原則

1. 優先透過 `IDbExecutor` 的 Query/Execute API 執行 Dapper 操作。
2. 必要時由 `IDbExecutor.Connection` 提供同一個 DI scope 連線，以維持既有 SQL 行為與交易邊界。
3. 已有交易物件（`SqlTransaction`）的路徑優先使用 `ExecuteInTx / QueryInTx / ExecuteScalarInTx` 系列 API，避免交易遺漏。
