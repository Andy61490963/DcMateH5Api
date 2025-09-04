
# 動態表單 & 權限系統資料表結構

* * *

## `FORM_FIELD_Master` — 表單主檔

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `SEQNO` | INT | 排序用（不可為 NULL） |
| `ID` | UUID | 主鍵，欄位設定唯一識別 |
| `FORM_NAME` | NVARCHAR(100) | 表單識別名稱，例如 `student_edit_form` |
| `BASE_TABLE_NAME` | NVARCHAR(100) | 實體資料表，如 `STUDENTS` |
| `VIEW_TABLE_NAME` | NVARCHAR(100) | 檢視表，如 `VW_STUDENT_FULL` |
| `BASE_TABLE_ID` | UUID | 主表對應 ID |
| `VIEW_TABLE_ID` | UUID | 檢視表對應 ID |
| `PRIMARY_KEY` | NVARCHAR(100) | 主表主鍵欄位 |
| `STATUS` | INT | 0=Draft, 1=Active |
| `SCHEMA_TYPE` | INT | 0=主表, 1=檢視表 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `FORM_FIELD_CONFIG` — 欄位設定

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `SEQNO` | INT | 排序用 |
| `ID` | UUID | 主鍵 |
| `FORM_FIELD_Master_ID` | UUID | 對應主檔 |
| `TABLE_NAME` | NVARCHAR(100) | 資料表名稱 |
| `COLUMN_NAME` | NVARCHAR(100) | 欄位名稱 |
| `DATA_TYPE` | NVARCHAR(100) | 資料型別 |
| `CONTROL_TYPE` | NVARCHAR(50) | 控制項類型 |
| `QUERY_DEFAULT_VALUE` | NVARCHAR(255) | 預設值 |
| `IS_EDITABLE` | BIT | 是否可編輯 |
| `IS_REQUIRED` | BIT | 是否必填 |
| `FIELD_ORDER` | INT | 顯示順序 |
| `CAN_QUERY` | BIT | 是否查詢條件 |
| `QUERY_COMPONENT` | NVARCHAR(50) | 查詢型別 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `FORM_FIELD_VALIDATION_RULE` — 欄位驗證規則

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `SEQNO` | INT | 排序 |
| `ID` | UUID | 主鍵 |
| `FIELD_CONFIG_ID` | UUID | 對應欄位設定 |
| `VALIDATION_TYPE` | NVARCHAR(50) | 驗證類型 |
| `VALIDATION_VALUE` | NVARCHAR(255) | 驗證值 |
| `MESSAGE_ZH` | NVARCHAR(255) | 中文訊息 |
| `MESSAGE_EN` | NVARCHAR(255) | 英文訊息 |
| `VALIDATION_ORDER` | INT | 驗證優先序 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `FORM_FIELD_DROPDOWN` — 下拉設定

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `SEQNO` | INT | 排序 |
| `ID` | UUID | 主鍵 |
| `FORM_FIELD_CONFIG_ID` | UUID | 對應欄位 |
| `ISUSESQL` | BIT | 是否使用 SQL |
| `DROPDOWNSQL` | NVARCHAR(255) | SQL 語法 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `FORM_FIELD_DROPDOWN_OPTIONS` — 下拉靜態選項

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `SEQNO` | INT | 排序 |
| `ID` | UUID | 主鍵 |
| `FORM_FIELD_DROPDOWN_ID` | UUID | 所屬下拉 |
| `OPTION_TABLE` | NVARCHAR(255) | 選項來源表 |
| `OPTION_VALUE` | NVARCHAR(255) | 儲存值 |
| `OPTION_TEXT` | NVARCHAR(255) | 顯示文字 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `FORM_FIELD_DROPDOWN_ANSWER` — 使用者下拉紀錄

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `SEQNO` | INT | 排序 |
| `ID` | UUID | 主鍵 |
| `ROW_ID` | NVARCHAR(255) | 指向主檔資料 |
| `FORM_FIELD_CONFIG_ID` | UUID | 對應欄位 |
| `FORM_FIELD_DROPDOWN_OPTIONS_ID` | UUID | 使用者選擇 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

# 權限系統

* * *

## `SYS_USER` — 使用者

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `ACCOUNT` | NVARCHAR(50) | 帳號 |
| `PASSWORD_HASH` | NVARCHAR(255) | 密碼雜湊 |
| `NAME` | NVARCHAR(100) | 姓名 |
| `EMAIL` | NVARCHAR(255) | 信箱 |
| `STATUS` | INT | 狀態 |
| `IS_DELETE` | BIT | 軟刪除 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `SYS_GROUP` — 群組

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `NAME` | NVARCHAR(100) | 群組名稱 |
| `DESCRIPTION` | NVARCHAR(255) | 群組描述 |
| `IS_SHARE` | BIT | 是否共用 |
| `STATUS` | INT | 狀態 |
| `IS_DELETE` | BIT | 軟刪除 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `SYS_FUNCTION` — 功能清單

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `PARENT_ID` | UUID | 父節點 |
| `NAME` | NVARCHAR(100) | 功能名稱 |
| `AREA` | NVARCHAR(100) | MVC Area |
| `CONTROLLER` | NVARCHAR(100) | Controller 名稱 |
| `SORT` | INT | 排序 |
| `IS_SHARE` | BIT | 是否共用 |
| `STATUS` | INT | 狀態 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `SYS_PERMISSION` — 功能權限

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `FUNCTION_ID` | UUID | 對應功能 |
| `ACTION_TYPE` | NVARCHAR(50) | 動作類型 (View/Edit/Delete...) |
| `DESCRIPTION` | NVARCHAR(255) | 說明 |
| `STATUS` | INT | 狀態 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

## `SYS_USER_GROUP` — 使用者群組對應

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `USER_ID` | UUID | 使用者 |
| `GROUP_ID` | UUID | 群組 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |

* * *

## `SYS_GROUP_FUNCTION_PERMISSION` — 群組功能權限

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `GROUP_ID` | UUID | 群組 |
| `FUNCTION_ID` | UUID | 功能 |
| `PERMISSION_ID` | UUID | 權限 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |

* * *

## `SYS_MENU` — 系統選單

| 欄位名稱 | 資料型別 | 說明 |
| --- | --- | --- |
| `ID` | UUID | 主鍵 |
| `NAME` | NVARCHAR(100) | 選單名稱 |
| `PARENT_ID` | UUID | 父節點 |
| `FUNCTION_ID` | UUID | 對應功能 |
| `SORT` | INT | 排序 |
| `ICON` | NVARCHAR(100) | Icon 名稱 |
| `STATUS` | INT | 狀態 |
| `CREATE_USER` | NVARCHAR(50) | 建立人 |
| `CREATE_TIME` | DATETIME | 建立時間 |
| `EDIT_USER` | NVARCHAR(50) | 修改人 |
| `EDIT_TIME` | DATETIME | 修改時間 |

* * *

# 關聯性 (Foreign Keys)

* `FORM_FIELD_CONFIG.FORM_FIELD_Master_ID` → `FORM_FIELD_Master.ID`
    
* `FORM_FIELD_VALIDATION_RULE.FIELD_CONFIG_ID` → `FORM_FIELD_CONFIG.ID`
    
* `FORM_FIELD_DROPDOWN.FORM_FIELD_CONFIG_ID` → `FORM_FIELD_CONFIG.ID`
    
* `FORM_FIELD_DROPDOWN_ANSWER.FORM_FIELD_CONFIG_ID` → `FORM_FIELD_CONFIG.ID`
    
* `FORM_FIELD_DROPDOWN_ANSWER.FORM_FIELD_DROPDOWN_OPTIONS_ID` → `FORM_FIELD_DROPDOWN_OPTIONS.ID`
    
* `SYS_USER_GROUP.USER_ID` → `SYS_USER.ID`
    
* `SYS_USER_GROUP.GROUP_ID` → `SYS_GROUP.ID`
    
* `SYS_GROUP_FUNCTION_PERMISSION.GROUP_ID` → `SYS_GROUP.ID`
    
* `SYS_GROUP_FUNCTION_PERMISSION.FUNCTION_ID` → `SYS_FUNCTION.ID`
    
* `SYS_GROUP_FUNCTION_PERMISSION.PERMISSION_ID` → `SYS_PERMISSION.ID`
    
* `SYS_MENU.FUNCTION_ID` → `SYS_FUNCTION.ID`
    
