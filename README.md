# DCMATE-H5-NEW — 動態表單（架構、Schema說明）

## 系統架構（Architecture）

本專案採用 **Clean Architecture 風格的多專案結構**，強制遵循以下依賴方向：

> **API (Web) → Infrastructure (Service) → Abstractions (Interface/Model)**  
> **Shared Library & DbExtensions (Data Access)**

### 專案結構說明

- **DcMateH5Api** (Web)
  - 應用程式入口，包含 Controllers (Areas) 與 DI 註冊 (Program.cs)。
  - 負責 Request 處理、驗證與 Response 格式化。
- **DcMateH5.Abstractions** (Core)
  - 定義所有介面 (Interfaces)、DTOs、ViewModels 與常數。
  - **不依賴任何實作細節**，是系統的核心合約層。
- **DcMateH5.Infrastructure** (Implementation)
  - 實作 Abstractions 定義的介面，包含商業邏輯 (Services)。
  - 負責組裝資料、處理流程、呼叫 Data Access 層。
- **DbExtensions** (Data Access)
  - 封裝資料庫存取邏輯，包含 `DbExecutor` 與 `SQLGenerateHelper`。
  - 負責 SQL 執行、交易控制與 Log。
- **DcMateClassLibrary** (Shared)
  - 共用的 Helper、Enums 與基礎模型。

---

## 系統分層總覽

```text
Client / Frontend
        ↓
Controller (DcMateH5Api)
        ↓
Service Interface (DcMateH5.Abstractions)
        ↓
Service Implementation (DcMateH5.Infrastructure)
        ↓
Data Access (DbExtensions)
        ↓
SQL Server
```

### 開發規範

1.  **Enum 優先**：狀態、類型一律使用 Enum (定義於 `DcMateClassLibrary`)，避免魔法數字。
2.  **Controller 輕量化**：禁止在 Controller 寫 SQL 或複雜邏輯，僅負責路由與參數驗證。
3.  **依賴注入 (DI)**：所有 Service 必須透過 Interface 注入，禁止直接 `new` Service。
4.  **非同步開發**：全線使用 `async/await`，資料庫操作務必傳遞 `CancellationToken`。

---

## Schema 說明：動態表單模組 (Dynamic Form)

### 1. 表單主檔：`FORM_FIELD_MASTER`

一筆資料代表一張表單的定義與資料來源。

| 欄位 | 說明 |
|---|---|
| `ID` | PK (Guid) |
| `FORM_NAME` / `FORM_CODE` | 表單名稱 / 代碼 |
| `SCHEMA_TYPE` | 類型 (主表/檢視表/TVF等) |
| `BASE_TABLE_ID` / `NAME` | 主表來源 |
| `DETAIL_TABLE_ID` / `NAME` | 明細表來源 |
| `VIEW_TABLE_ID` / `NAME` | 檢視表來源 |
| `MAPPING_TABLE_ID` / `NAME` | Mapping 表來源 (中介表) |
| `TVF_TABLE_ID` / `NAME` | **[NEW]** Table Value Function 來源 |
| `FUNCTION_TYPE` | 功能類型 (Enum) |
| `STATUS` | 狀態 (Active/Draft/Disable) |

### 2. 資料存取層 (DbExecutor / SQLGenerateHelper)

資料存取邏輯位於 `DbExtensions` 專案中。

#### DbExecutor (Dapper Wrapper)
-   負責執行 raw SQL。
-   提供 `QueryAsync`, `ExecuteAsync` 等方法。
-   支援 Transaction 與 SQL Logging。
-   **使用方式**：由 DI 注入 `IDbExecutor`。

#### SQLGenerateHelper (ORM Helper)
-   基於 `DataAnnotations` (在 DTO 上) 自動產生 CRUD SQL。
-   提供 `InsertAsync`, `SelectAsync`, `UpdateById`, `DeleteWhereAsync` 等。
-   **特點**：
    -   `[Table]`: 指定對應 Table。
    -   `[Key]`: 指定 PK。
    -   `[Column]`: 指定欄位名稱。

### 3. 交易控制 (Transaction)
-   簡單操作可使用 `DbExecutor` 或 `SQLGenerateHelper` 的 Tx 版本方法。
-   **複雜交易**：建議在 Service 層開啟 `Using var tx = ...` 並傳遞給 Repositories/Helpers，確保跨操作的原子性。
