# DCMATE-H5-NEW — 動態表單（架構、Schema說明）


# 系統架構（Architecture）

本專案採用 **多專案（Multi-Project）+ Area 模組化** 架構，並強制遵循以下開發規範：

> **Enum → Area → Controller → Interface → Service → DbExecutor / SQLGenerateHelper**

此規範的目的在於：
- 消滅魔法字串 / 魔法數字
- 避免 SQL 與商業邏輯散落在 Controller
- 強制分層，確保可維護性與可測試性
- 統一資料存取、交易與 Logging 行為

---

### 系統分層總覽

```text
Client / Frontend
        ↓
Controller（Area）
        ↓
Service Interface
        ↓
Service（商業邏輯）
        ↓
DbExecutor / SQLGenerateHelper
        ↓
SQL Server
```

* * *

### Enum（狀態與行為定義層）

* 所有 **狀態、類型、行為** 一律使用 Enum 表示

* DB 欄位為 `int` 時，程式端必須對應 Enum，不可直接使用裸值


常見 Enum 類型包含：

* Form：`SchemaType`、`FormControlType`、`ValidationType`、`QueryComponentType`

* System：`ActionType`（View / Edit / Delete / Approve…）

* ApiResult / SystemError


規範：

* Controller / Service 禁止傳遞裸 `int` 或魔法字串

* Enum 命名必須具備業務語意


* * *

### Area（功能模組邊界）

系統以 **ASP.NET Core Area** 作為功能模組切分單位，例如：

* `Form`：動態表單（Designer / Submit / Master-Detail / Mapping）

* `Permission`（或未來擴充）：權限、選單、功能控管

* `ApiStats`：系統監控 / 統計


每個 Area 必須是**獨立且封閉的功能模組**。

* * *

### Controller（API 入口層）

Controller 職責僅限於：

* API Routing

* Request Model 驗證

* 權限 ActionType 檢核

* 呼叫對應 Service

* 統一回傳結果格式


嚴格禁止：

* 撰寫 SQL

* 處理複雜商業流程

* 直接操作資料表結構


* * *

### Interface（Service 合約層）

* 每個 Service 必須有對應的 Interface（`IxxxService`）

* Controller **只能依賴 Interface，不可依賴實作**

* 方便未來抽換實作、單元測試與 Mock


* * *

### Service（商業邏輯核心）

Service 負責：

* 動態表單組裝邏輯

* 驗證規則處理

* Dropdown Answer 寫入 / 查詢

* 主表 / 明細 / Mapping 的交易控制

* 組裝 ViewModel 回傳給 Controller


規範：

* Service 層才允許呼叫 DbExecutor / SQLGenerateHelper

* 多表寫入必須使用 Transaction

* 查詢預設需排除軟刪除資料（`IS_DELETE = 0`）


* * *

### 資料存取層（共用 Library）

* **DbExecutor**

    * Dapper 唯一入口

    * 負責 Query / Execute / Transaction / SQL Logging

* **SQLGenerateHelper**

    * 基於 Attribute 的 CRUD Helper

    * 提供 Insert / Select / Soft Delete / Fluent UpdateById

* **WhereBuilder**

    * AND-only 條件組裝

    * 全參數化、防 SQL Injection

    * 防止誤刪 / 誤更整張表


Service 不得：

* 自行 new `SqlConnection`

* 直接呼叫 Dapper


* * *


#### 2) 複雜 SQL 的交易必須使用 DI 注入的 `_CON`

當使用手寫 SQL 時，若涉及 **多筆寫入 / 多張表 / 一致性要求**，必須使用
由 **依賴注入（DI）提供的 `_CON`（SqlConnection / IDbConnection）** 進行交易控制。

規範如下：

- `_CON` 必須由 DI 注入（Scoped / Transient），禁止自行 `new SqlConnection`
- 同一筆業務流程中的所有 SQL，必須共用 **同一個 connection + transaction**
- 交易控制（Begin / Commit / Rollback）必須在 Service 層完成
- Controller 不得處理任何交易行為
- _CON 的生命週期必須為 Scoped

範例（手寫 SQL + `_CON` 交易）：

```csharp
public async Task DoSomethingComplexAsync(InputModel input, CancellationToken ct)
{
    await _CON.OpenAsync(ct);
    await using var tx = await _CON.BeginTransactionAsync(ct);

    try
    {
        await _CON.ExecuteAsync(
            @"UPDATE TABLE_A SET COL = @Val WHERE ID = @Id;",
            new { Val = input.Val, Id = input.Id },
            transaction: tx);

        await _CON.ExecuteAsync(
            @"INSERT INTO TABLE_B (A_ID, VALUE) VALUES (@Id, @Value);",
            new { Id = input.Id, Value = input.Value },
            transaction: tx);

        await tx.CommitAsync(ct);
    }
    catch
    {
        await tx.RollbackAsync(CancellationToken.None);
        throw;
    }
}
```

* * *

### 架構鐵則（必遵守）

*  Enum 必須存在

*  Controller 必須保持薄

*  商業邏輯集中在 Service

*  禁止 dynamic

*  禁止魔法字串


---

# Schema說明：

1. **動態表單（Dynamic Form）**
    - 以 `FORM_FIELD_MASTER` 定義「一張表單」對應的資料來源（主表/明細表/檢視表/Mapping）
    - 以 `FORM_FIELD_CONFIG` 定義欄位渲染與行為（控制項、可編輯、必填、查詢元件等）
    - 以 `FORM_FIELD_VALIDATION_RULE` 定義欄位驗證（required/min/max/regex…）
    - 以 `FORM_FIELD_DROPDOWN` / `FORM_FIELD_DROPDOWN_OPTIONS` 定義下拉選單來源（SQL / 靜態）
    - 以 `FORM_FIELD_DROPDOWN_ANSWER` 儲存「使用者選擇的 option id」（避免顯示文字與儲存值混在一起）

---

## 1) 動態表單模組（Dynamic Form）

### 1.1 表單主檔：`FORM_FIELD_MASTER`

一筆代表一張表單的「資料來源與關聯設定」。

已經支援：
- 主表（Base）
- 明細表（Detail）
- 檢視表（View）
- Mapping 表（用於多對多或中介關係）
- 主明細關聯欄位（Mapping FK / Relation 欄位）

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | uniqueidentifier | PK |
| `FORM_NAME` | nvarchar(255) | 表單名稱/識別 |
| `FORM_CODE` | nvarchar(255) | 表單代碼（可做版本/分類） |
| `FORM_DESCRIPTION` | nvarchar(255) | 描述 |
| `BASE_TABLE_NAME` | nvarchar(255) | 主表表名 |
| `DETAIL_TABLE_NAME` | nvarchar(255) | 明細表表名 |
| `VIEW_TABLE_NAME` | nvarchar(255) | 檢視表表名 |
| `MAPPING_TABLE_NAME` | nvarchar(255) | Mapping 表表名 |
| `BASE_TABLE_ID` / `DETAIL_TABLE_ID` / `VIEW_TABLE_ID` / `MAPPING_TABLE_ID` | uniqueidentifier | 對應的表/檢視 metadata id（若有） |
| `MAPPING_BASE_FK_COLUMN` / `MAPPING_DETAIL_FK_COLUMN` | nvarchar(255) | Mapping 表中 FK 欄位 |
| `MAPPING_BASE_COLUMN_NAME` / `MAPPING_DETAIL_COLUMN_NAME` | nvarchar(255) | Mapping 表對應欄位 |
| `FUNCTION_TYPE` | int | 功能類型（enum） |
| `STATUS` | int | 狀態（例如 Draft/Active） |
| `SCHEMA_TYPE` | int | schema 類型（例如 主表/檢視表） |
| `IS_DELETE` | bit | 是否已刪除 |
| `ROW_VERSION` | timestamp | 樂觀鎖（Concurrency） |
| `CREATE_USER` / `EDIT_USER` | uniqueidentifier | 建立/修改者 |
| `CREATE_TIME` / `EDIT_TIME` | datetime | 建立/修改時間 |


---

### 1.2 欄位設定：`FORM_FIELD_CONFIG`

一筆代表表單的一個欄位

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | uniqueidentifier | PK |
| `FORM_FIELD_MASTER_ID` | uniqueidentifier | FK → `FORM_FIELD_MASTER.ID` |
| `TABLE_NAME` | nvarchar(100) | 欄位來源表 |
| `COLUMN_NAME` | nvarchar(100) | 欄位名稱 |
| `DATA_TYPE` | nvarchar(100) | 資料型別（文字描述） |
| `CONTROL_TYPE` | nvarchar(50) | 控制項（enum）（TextBox/Dropdown/Date…） |
| `CAN_QUERY` | bit | 是否可作查詢條件 |
| `QUERY_COMPONENT` | int | 查詢元件類型（enum） |
| `QUERY_CONDITION` | nvarchar(50) | 查詢條件（enum）（=, like, between…） |
| `QUERY_DEFAULT_VALUE` | nvarchar(255) | 查詢預設值 |
| `IS_EDITABLE` | bit | 是否可編輯（預設 1） |
| `IS_REQUIRED` | bit | 是否必填 |
| `FIELD_ORDER` | int | 顯示順序（預設 0） |
| `IS_DELETE` | bit | 是否已刪除 |
| `CREATE_TIME` / `EDIT_TIME` | datetime | 建立/修改時間 |


---

### 1.3 欄位驗證：`FORM_FIELD_VALIDATION_RULE`

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | uniqueidentifier | PK |
| `FIELD_CONFIG_ID` | uniqueidentifier | FK → `FORM_FIELD_CONFIG.ID` |
| `VALIDATION_TYPE` | int | 驗證類型（enum） |
| `VALIDATION_VALUE` | nvarchar(255) | 驗證值（例如 min=3、regex pattern） |
| `MESSAGE_ZH` / `MESSAGE_EN` | nvarchar(255) | 多語訊息 |
| `VALIDATION_ORDER` | int | 優先序（預設 0） |
| `IS_DELETE` | bit | 是否已刪除 |

---

### 1.4 下拉選單：`FORM_FIELD_DROPDOWN` / `FORM_FIELD_DROPDOWN_OPTIONS`

#### `FORM_FIELD_DROPDOWN`
| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | uniqueidentifier | PK |
| `FORM_FIELD_CONFIG_ID` | uniqueidentifier | FK → `FORM_FIELD_CONFIG.ID` |
| `ISUSESQL` | bit | 是否用 SQL 動態產生 |
| `DROPDOWNSQL` | nvarchar(255) | SQL 語法（建議只允許 select） |
| `IS_QUERY_DROPDOWN` | bit | 是否作為查詢用的下拉 |
| `IS_DELETE` | bit | 是否已刪除 |

#### `FORM_FIELD_DROPDOWN_OPTIONS`
| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | uniqueidentifier | PK |
| `FORM_FIELD_DROPDOWN_ID` | uniqueidentifier | FK → `FORM_FIELD_DROPDOWN.ID` |
| `OPTION_TABLE` | nvarchar(255) | 選項來源表（可選填） |
| `OPTION_VALUE` | nvarchar(255) | 儲存值 |
| `OPTION_TEXT` | nvarchar(255) | 顯示文字 |
| `IS_DELETE` | bit | 是否已刪除 |

備註：
- `DROPDOWNSQL` **是高風險點**：限制只能 SELECT + 禁止分號/註解

---

### 1.5 使用者下拉回答：`FORM_FIELD_DROPDOWN_ANSWER`

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | uniqueidentifier | PK |
| `ROW_ID` | nvarchar(255) | 指向主檔資料的 id（或 relation key） |
| `FORM_FIELD_CONFIG_ID` | uniqueidentifier | FK → `FORM_FIELD_CONFIG.ID` |
| `FORM_FIELD_DROPDOWN_OPTIONS_ID` | uniqueidentifier | FK → `FORM_FIELD_DROPDOWN_OPTIONS.ID` |
| `IS_DELETE` | bit | 軟刪除 |

重點：
- 這張表的用途是「把 Dropdown 的選擇記錄成 option id」，可以：
    - 顯示時可以 join `OPTION_TEXT`
    - 儲存時保持正規化，不會出現 “值/文字混用”
- 查詢列表（List）時，是以「FORM_FIELD_DROPDOWN_ANSWER.ID 覆蓋原始值」


---

## 2) 資料存取共用 Library（DbExecutor / SQLGenerateHelper）

本專案資料存取層主要由兩個共用元件組成：

- **DbExecutor**：包裝 Dapper 的 Execute / Query / Transaction，並內建 SQL Log（最佳努力）
- **SQLGenerateHelper**：基於 DataAnnotations 反射產生 CRUD SQL，提供 Insert / Select / Delete / Fluent UpdateById，並搭配 WhereBuilder 組條件（全程參數化）

> 目的：讓 Service 層專注在「商業邏輯」，把「SQL 執行、交易、紀錄、參數化、防呆」集中處理，避免每個地方都手刻一套。

---

### 2.1 DbExecutor（Dapper Wrapper + SQL Logging）

`DbExecutor` 是一個包裝 Dapper 的資料存取層，對外提供：

- **Query**：`QueryAsync<T>() / QueryFirstOrDefaultAsync<T>() / QuerySingleOrDefaultAsync<T>()`
- **Execute**：`ExecuteAsync()`（Insert/Update/Delete）、`ExecuteScalarAsync<T>()`
- **Transaction**：`TxAsync(...) / TxAsync<T>(...)`
- **Tx 版本**：`QueryInTxAsync<T>() / ExecuteInTxAsync()`…（使用既有 `SqlConnection/SqlTransaction`）

#### 重點

1) **所有參數都走 Dapper 參數化**
- 避免 SQL Injection
- `DynamicParameters` 也可被序列化成 log

2) **內建 SQL 執行紀錄（Logging）**
- 每次 Query/Execute 都會建立 `SqlLogEntry`，記錄：
    - `RequestId`（CorrelationId）
    - `ExecutedAt / DurationMs`
    - `SqlText`
    - `Parameters`（JSON，且有長度上限）
    - `AffectedRows`
    - `UserId`（JWT sub / NameIdentifier）
    - `IpAddress`

3) **Logging 是「最佳努力」**
- Log 寫入失敗會吞掉，不會影響主流程
- 主流程的例外仍會正常 throw（不會被 log 吃掉）

4) **同一個 Request 共用 CorrelationId**
- `DbExecutor.GetCorrelationId(HttpContext)` 會把 Guid 存在 `HttpContext.Items["CorrelationId"]`
- 讓你一個 API request 內的所有 SQL log 都可以串起來追（除錯超有用）

#### 注意事項（很重要）

- `DefaultTimeoutSeconds = 30`：若遇到報表/大查詢，請在呼叫端顯式傳入 `timeoutSeconds`
- `MaxParameterLength = 4000`：參數 JSON 會截斷，避免 log 表被塞爆
- **DbExecutor 不會自動幫你過濾刪除Flag**：`IS_DELETE` 條件要由上層（WhereBuilder/SQL）自行帶入

---

### 2.2 SQLGenerateHelper（Reflection-based CRUD + Fluent UpdateById）

`SQLGenerateHelper` 是「精簡版 ORM Helper」：  
只依賴 `DataAnnotations` 來推導 Table/Column/Key/Timestamp，並產生 SQL。

依賴的 Attribute：

- `[Table]`：指定資料表（可含 Schema）
- `[Column]`：指定欄位名稱（不指定就用 PropertyName）
- `[Key]`：指定主鍵欄位（必須存在）
- `[Timestamp]`：rowversion 欄位（用於樂觀鎖，現在都還沒有用到）

#### 核心能力

1) **Insert**
- `InsertAsync<T>(entity)`：短連線插入
- `InsertInTxAsync<T>(conn, tx, entity)`：交易內插入
- `InsertAndGetIdentityAsync<T>(entity)`：插入並取 `SCOPE_IDENTITY()`

2) **Select**
- `SelectAsync<T>()`：全表查詢（不過濾軟刪除）
- `SelectWhereAsync<T>(WhereBuilder<T>)`
- `SelectFirstOrDefaultAsync<T>(WhereBuilder<T>)`
- 皆有 Tx 版本

3) **Delete（Soft Delete）**
- `DeleteWhereAsync<T>(where)`：更新 `IS_DELETE = 1`
- `DeletePhysicalWhereInTxAsync<T>(where)`：Tx 版物理刪除（強制必須有 WHERE，防止全表刪除）

4) **Update（重點）**
- 全表更新：`UpdateAllByIdAsync<T>(entity, mode, ...)`
- 單一更新：`UpdateById<T>(id)`

Fluent Update 範例：

```csharp
await _sql.UpdateById<FormFieldConfig>(id)
    .Set(x => x.IS_REQUIRED, true)
    .Set(x => x.EDIT_TIME, DateTime.Now)
    .Audit()
    .ExecuteAsync(ct);
