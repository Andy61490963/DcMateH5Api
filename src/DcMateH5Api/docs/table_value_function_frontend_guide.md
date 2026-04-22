# Table Value Function 前端串接說明

Table Value Function, 以下簡稱 TVF, 是用 SQL Server Table-Valued Function 做動態查詢頁。

它不是一般 CRUD 表單。前端的主要工作是：

- 讓使用者選擇一個 TVF 設定檔
- 收集 TVF 需要的參數
- 呼叫查詢 API
- 依照後端回傳的動態欄位顯示資料

## 基本概念

假設 DB 有一支 TVF：

```sql
dbo.fn_GetEqpData(@EQP_NO nvarchar(50), @S_TIME datetime, @E_TIME datetime)
```

後端會把它拆成兩種欄位：

```text
TVF 參數：
@EQP_NO
@S_TIME
@E_TIME

TVF 回傳欄位：
EQP_NAME
STATUS
REPORT_TIME
...
```

前端查詢時，`TvfParameters` 會被傳進 TVF：

```sql
SELECT *
FROM [dbo].[fn_GetEqpData](@tvf_p0, @tvf_p1, @tvf_p2)
```

`Conditions` 則是對 TVF 回傳結果再加查詢條件：

```sql
WHERE [STATUS] = @w0
```

所以請把這兩個概念分開：

```text
TvfParameters = 傳給 TVF function 本身的參數
Conditions = TVF 執行完後，對結果資料再過濾
```

## 前端查詢流程

### 1. 取得可用的 TVF 設定檔

```http
GET /Form/FormTableValueFunction/masters
```

回傳範例：

```json
[
  {
    "Id": "0f68fd7d-43ef-45f4-8d10-1a9b4e5f9e11",
    "TableFunctionValueId": "2d0c6d24-74a0-43a5-ae9f-d334fb16f8be",
    "FormName": "設備資料查詢",
    "TableFunctionValueName": "fn_GetEqpData",
    "Parameter": ["EQP_NO", "S_TIME", "E_TIME"]
  }
]
```

欄位說明：

| 欄位 | 說明 |
| --- | --- |
| `Id` | 正式 TVF 設定檔 ID。查詢資料時請把這個值放到 `FormMasterId`。 |
| `TableFunctionValueId` | TVF 欄位定義 master ID，通常前端查詢資料不需要用。 |
| `FormName` | 前端顯示名稱。 |
| `TableFunctionValueName` | SQL Server TVF 名稱。 |
| `Parameter` | TVF 需要的參數名稱。前端可用來產生查詢輸入欄位。 |

### 2. 呼叫 TVF 查詢 API

```http
POST /Form/FormTableValueFunction/search
Content-Type: application/json
```

Body 範例：

```json
{
  "FormMasterId": "0f68fd7d-43ef-45f4-8d10-1a9b4e5f9e11",
  "Page": 1,
  "PageSize": 20,
  "TvfParameters": {
    "EQP_NO": "MC1",
    "S_TIME": "2026-04-15 08:00:00",
    "E_TIME": "2026-04-15 17:00:00"
  },
  "Conditions": [
    {
      "Column": "STATUS",
      "ConditionType": 1,
      "Value": "RUN"
    }
  ],
  "OrderBys": [
    {
      "Column": "REPORT_TIME",
      "Direction": 1
    }
  ]
}
```

最小 Body：

```json
{
  "FormMasterId": "0f68fd7d-43ef-45f4-8d10-1a9b4e5f9e11",
  "Page": 1,
  "PageSize": 20,
  "TvfParameters": {
    "EQP_NO": "MC1",
    "S_TIME": "2026-04-15 08:00:00",
    "E_TIME": "2026-04-15 17:00:00"
  }
}
```

Request 欄位說明：

| 欄位 | 必填 | 說明 |
| --- | --- | --- |
| `FormMasterId` | 是 | `/masters` 回傳的 `Id`。 |
| `Page` | 否 | 分頁頁碼，從 `1` 開始。未傳預設為 `0`，但建議前端固定傳 `1`。 |
| `PageSize` | 否 | 每頁筆數。未傳預設為 `20`。 |
| `TvfParameters` | 視 TVF 而定 | TVF function 的參數值。key 可以帶不含 `@` 的名稱，例如 `EQP_NO`。 |
| `Conditions` | 否 | 對 TVF 回傳結果再過濾。 |
| `OrderBys` | 否 | 對 TVF 回傳結果排序。 |

## TvfParameters

`TvfParameters` 是傳給 SQL TVF 的參數。

例如 TVF 是：

```sql
dbo.fn_GetEqpData(@EQP_NO, @S_TIME, @E_TIME)
```

前端可傳：

```json
{
  "TvfParameters": {
    "EQP_NO": "MC1",
    "S_TIME": "2026-04-15 08:00:00",
    "E_TIME": "2026-04-15 17:00:00"
  }
}
```

注意事項：

- key 可以不用加 `@`，後端會做正規化。
- value 會依照 SQL Server schema 的資料型別轉型。
- 如果 `TvfParameters` 沒有傳，後端會嘗試使用欄位設定裡的 `TVF_CURRENT_VALUE` 當預設值。
- 如果該 TVF 沒有任何參數設定，且前端也沒傳 `TvfParameters`，後端會回 `400 BadRequest`。

## Conditions

`Conditions` 是對 TVF 回傳欄位做查詢條件。

範例：

```json
{
  "Conditions": [
    {
      "Column": "STATUS",
      "ConditionType": 1,
      "Value": "RUN"
    },
    {
      "Column": "REPORT_TIME",
      "ConditionType": 3,
      "Value": "2026-04-15 08:00:00",
      "Value2": "2026-04-15 17:00:00"
    }
  ]
}
```

`ConditionType` 對照：

| 值 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `None` | 不使用 |
| `1` | `Equal` | 等於 |
| `2` | `Like` | 模糊查詢，後端會自動包 `%value%` |
| `3` | `Between` | 介於 `Value` 和 `Value2` |
| `4` | `GreaterThan` | 大於 |
| `5` | `GreaterThanOrEqual` | 大於等於 |
| `6` | `LessThan` | 小於 |
| `7` | `LessThanOrEqual` | 小於等於 |
| `8` | `In` | 包含於 `Values` |
| `9` | `NotEqual` | 不等於 |
| `10` | `NotIn` | 不包含於 `Values` |

`In` 範例：

```json
{
  "Column": "STATUS",
  "ConditionType": 8,
  "Values": ["RUN", "IDLE"]
}
```

條件注意事項：

- `Column` 必須是 TVF 回傳欄位，不是 TVF 參數。
- `Column` 只允許英數字與底線。
- 如果欄位不存在，後端會忽略該條件。
- `DataType` 目前前端可以不用傳，後端會用 TVF schema 的欄位型別轉換。

## OrderBys

範例：

```json
{
  "OrderBys": [
    {
      "Column": "REPORT_TIME",
      "Direction": 1
    }
  ]
}
```

`Direction` 對照：

| 值 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `Asc` | 小到大 |
| `1` | `Desc` | 大到小 |

注意事項：

- `Column` 必須是 TVF 回傳欄位。
- 分頁時 SQL Server 需要 `ORDER BY`。如果前端沒有傳 `OrderBys`，後端會使用 `ORDER BY (SELECT NULL)`。

## 查詢回傳格式

`POST /Form/FormTableValueFunction/search` 回傳：

```json
[
  {
    "FormMasterId": "0f68fd7d-43ef-45f4-8d10-1a9b4e5f9e11",
    "FormName": "設備資料查詢",
    "Fields": [
      {
        "FieldConfigId": "8c4a9b27-6696-42c0-9ff6-4a75abfc1e36",
        "Column": "EQP_NAME",
        "DISPLAY_NAME": "設備名稱",
        "DATA_TYPE": "nvarchar",
        "CONTROL_TYPE": 0,
        "DefaultValue": null,
        "IS_REQUIRED": false,
        "IS_EDITABLE": false,
        "IS_DISPLAYED": true,
        "QUERY_COMPONENT": 0,
        "QUERY_CONDITION": 0,
        "CAN_QUERY": true,
        "OptionList": [],
        "CurrentValue": "MC1 設備"
      },
      {
        "FieldConfigId": "82bc4bbf-24d2-4cb5-9dc8-893dbba58f27",
        "Column": "STATUS",
        "DISPLAY_NAME": "狀態",
        "DATA_TYPE": "nvarchar",
        "CONTROL_TYPE": 0,
        "DefaultValue": null,
        "IS_REQUIRED": false,
        "IS_EDITABLE": false,
        "IS_DISPLAYED": true,
        "QUERY_COMPONENT": 0,
        "QUERY_CONDITION": 0,
        "CAN_QUERY": true,
        "OptionList": [],
        "CurrentValue": "RUN"
      }
    ]
  }
]
```

前端顯示建議：

- 一筆 array item 代表一筆 TVF 查詢結果。
- 每筆資料的欄位都在 `Fields` 裡。
- 欄位標題用 `DISPLAY_NAME`。
- 欄位值用 `CurrentValue`。
- 只顯示 `IS_DISPLAYED = true` 的欄位。
- 欄位順序後端已依設定排序，前端照 `Fields` 順序顯示即可。

## 前端實作建議

### 查詢頁初始化

1. 呼叫 `GET /Form/FormTableValueFunction/masters`
2. 讓使用者選擇一個 TVF 設定檔
3. 依照選到的設定檔 `Parameter` 產生參數輸入欄位
4. 使用者按查詢時呼叫 `POST /Form/FormTableValueFunction/search`
5. 依照回傳的 `Fields` 動態產生表格欄位與資料列

### 表格欄位產生方式

可以用第一筆資料的 `Fields` 產生 columns：

```ts
const rows = response.data;
const columns = rows[0]?.Fields
  .filter(field => field.IS_DISPLAYED)
  .map(field => ({
    key: field.Column,
    title: field.DISPLAY_NAME || field.Column
  })) ?? [];
```

資料列可以把 `Fields` 攤平成 object：

```ts
const tableRows = rows.map(row => {
  const item: Record<string, unknown> = {};

  row.Fields
    .filter(field => field.IS_DISPLAYED)
    .forEach(field => {
      item[field.Column] = field.CurrentValue;
    });

  return item;
});
```

### 查詢參數組裝

```ts
const request = {
  FormMasterId: selectedMaster.Id,
  Page: 1,
  PageSize: 20,
  TvfParameters: {
    EQP_NO: formValues.EQP_NO,
    S_TIME: formValues.S_TIME,
    E_TIME: formValues.E_TIME
  },
  Conditions: [
    {
      Column: "STATUS",
      ConditionType: 1,
      Value: formValues.STATUS
    }
  ],
  OrderBys: [
    {
      Column: "REPORT_TIME",
      Direction: 1
    }
  ]
};
```

## 常見問題

### `TvfParameters` 和 `Conditions` 有什麼差別？

`TvfParameters` 是 TVF function 的 input。

`Conditions` 是 TVF 回傳資料後再加上的 `WHERE`。

例如：

```json
{
  "TvfParameters": {
    "EQP_NO": "MC1"
  },
  "Conditions": [
    {
      "Column": "STATUS",
      "ConditionType": 1,
      "Value": "RUN"
    }
  ]
}
```

概念上會像：

```sql
SELECT *
FROM dbo.fn_GetEqpData(@EQP_NO)
WHERE STATUS = 'RUN'
```

### 查詢 API 為什麼回傳 array，不是 `{ items, total }`？

目前 TVF 查詢回傳型別是 `List<FormTvfListDataViewModel>`。

也就是：

```json
[
  {
    "FormMasterId": "...",
    "FormName": "...",
    "Fields": []
  }
]
```

目前沒有回傳總筆數或總頁數。

### `/masters` 的 `Parameter` 是空的怎麼辦？

前端仍可手動依 TVF 需要的參數送 `TvfParameters`。

如果畫面需要自動產生參數欄位，但 `/masters` 的 `Parameter` 回空陣列，請回報後端檢查 TVF 欄位設定是否有正確標記 `IS_TVF_QUERY_PARAMETER`。

### `Column` 可以傳任何字串嗎？

不行。`Conditions` 和 `OrderBys` 的 `Column` 必須是 TVF 回傳欄位，而且只允許英數字與底線。

### 日期格式要怎麼傳？

建議傳 SQL Server 可解析的日期字串：

```json
"2026-04-15 08:00:00"
```

也可以依前端日期元件統一輸出 ISO 格式，但要確認 DB function 的參數型別能正確轉換。

