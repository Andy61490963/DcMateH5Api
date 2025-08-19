
# 📋 動態表單資料表結構說明

## 📌 `FORM_FIELD_Master` — 表單主檔

| 欄位名稱 | 資料型別 | 說明 |
|----------|----------|------|
| `SEQNO` | INT | 排序用（不可為 NULL） |
| `ID` | UUID | 主鍵，欄位設定的唯一識別編號 |
| `FORM_NAME` | NVARCHAR(100) | 表單識別名稱，例如 `student_edit_form` |
| `BASE_TABLE_NAME` | NVARCHAR(100) | 寫入/更新的實體資料表，如 `STUDENTS` |
| `VIEW_TABLE_NAME` | NVARCHAR(100) | 僅供展示的檢視表，如 `VW_STUDENT_FULL` |
| `BASE_TABLE_ID` | UUID | 寫入資料表對應的主表 ID |
| `VIEW_TABLE_ID` | UUID | 檢視表對應的主表 ID |
| `STATUS` | INT | 表單狀態（尚未儲存前為 Draft）|
| `SCHEMA_TYPE` | INT | 判斷為主表還是檢視表 |

## 📌 `FORM_FIELD_CONFIG` — 欄位設定檔

| 欄位名稱 | 資料型別 | 說明 |
|----------|----------|------|
| `SEQNO` | INT | 排序用 |
| `ID` | UUID | 主鍵，欄位設定唯一識別 |
| `FORM_FIELD_Master_ID` | UUID | 對應主檔 `FORM_FIELD_Master.ID` |
| `TABLE_NAME` | NVARCHAR(100) | 對應的資料表名稱（如 `STUDENTS`） |
| `COLUMN_NAME` | NVARCHAR(100) | 資料表的欄位名稱 |
| `DATA_TYPE` | NVARCHAR(100) | 欄位資料型別（如 `Nvarchar`） |
| `CONTROL_TYPE` | NVARCHAR(50) | 控制項類型：input / select / textarea / checkbox |
| `DEFAULT_VALUE` | NVARCHAR(255) | 預設值，可為靜態或程式產生 |
| `IS_EDITABLE` | BIT | 是否可編輯（預設為 true） |
| `IS_REQUIRED` | BIT | 是否必填（預設為 true） |
| `FIELD_ORDER` | INT | 頁面上顯示順序（預設為 0） |
| `CAN_QUERY` | BIT | 是否允許查詢條件欄位（預設為 false） |
| `QUERY_CONDITION_TYPE` | NVARCHAR(50) | 前台顯示的查詢類型對應 Enum: QueryConditionType |
| `CREATE_USER` | NVARCHAR(50) | 建立者帳號 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 最後修改人帳號 |
| `EDIT_TIME` | DATETIME | 最後修改時間 |

## 📌 `FORM_FIELD_VALIDATION_RULE` — 欄位驗證規則

| 欄位名稱 | 資料型別 | 說明 |
|----------|----------|------|
| `SEQNO` | INT | 排序用 |
| `ID` | UUID | 主鍵，驗證規則唯一識別 |
| `FIELD_CONFIG_ID` | UUID | 對應 `FORM_FIELD_CONFIG.ID` |
| `VALIDATION_TYPE` | NVARCHAR(50) | 驗證類型：required / max / min / regex / number / email |
| `VALIDATION_VALUE` | NVARCHAR(255) | 驗證值，如最大長度、正則等 |
| `MESSAGE_ZH` | NVARCHAR(255) | 錯誤訊息（中文） |
| `MESSAGE_EN` | NVARCHAR(255) | 錯誤訊息（英文） |
| `VALIDATION_ORDER` | INT | 驗證優先順序（越小越先執行） |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 最後修改人 |
| `EDIT_TIME` | DATETIME | 最後修改時間 |

## 📌 `FORM_FIELD_DROPDOWN` — 下拉設定（來源 SQL or 靜態）

| 欄位名稱 | 資料型別 | 說明 |
|----------|----------|------|
| `SEQNO` | INT | 排序用 |
| `ID` | UUID | 主鍵，靜態下拉唯一識別 |
| `FORM_FIELD_CONFIG_ID` | UUID | 對應哪個欄位的下拉 |
| `ISUSESQL` | BIT | 是否使用 SQL（預設 true） |
| `DROPDOWNSQL` | NVARCHAR(255) | SQL 查詢語法（僅限 `SELECT`） |

## 📌 `FORM_FIELD_DROPDOWN_OPTIONS` — 下拉靜態選項

| 欄位名稱 | 資料型別 | 說明 |
|----------|----------|------|
| `SEQNO` | INT | 排序用 |
| `ID` | UUID | 主鍵，靜態選項唯一識別 |
| `FORM_FIELD_DROPDOWN_ID` | UUID | 所屬下拉 |
| `OPTION_TABLE` | NVARCHAR(255) | SQL 選項的來源表 |
| `OPTION_VALUE` | NVARCHAR(255) | 選項 ID（儲存值） |
| `OPTION_TEXT` | NVARCHAR(255) | 顯示用文字 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 最後修改人 |
| `EDIT_TIME` | DATETIME | 最後修改時間 |

## 📌 `FORM_FIELD_DROPDOWN_ANSWER` — 使用者下拉選擇紀錄

| 欄位名稱 | 資料型別 | 說明 |
|----------|----------|------|
| `SEQNO` | INT | 排序用 |
| `ID` | UUID | 主鍵，紀錄唯一 ID |
| `ROW_ID` | NVARCHAR(255) | 指向主檔資料 ID |
| `FORM_FIELD_CONFIG_ID` | UUID | 哪個欄位的選擇紀錄 |
| `FORM_FIELD_DROPDOWN_OPTIONS_ID` | UUID | 使用者選擇的選項 ID |

## 🔗 資料表關聯（Foreign Keys）

- `FORM_FIELD_CONFIG.FORM_FIELD_Master_ID` → `FORM_FIELD_Master.ID`
- `FORM_FIELD_VALIDATION_RULE.FIELD_CONFIG_ID` → `FORM_FIELD_CONFIG.ID`
- `FORM_FIELD_DROPDOWN.FORM_FIELD_CONFIG_ID` → `FORM_FIELD_CONFIG.ID`
- `FORM_FIELD_DROPDOWN_ANSWER.FORM_FIELD_CONFIG_ID` → `FORM_FIELD_CONFIG.ID`
- `FORM_FIELD_DROPDOWN_ANSWER.FORM_FIELD_DROPDOWN_OPTIONS_ID` → `FORM_FIELD_DROPDOWN_OPTIONS.ID`
