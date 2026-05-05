# FormView 前端串接說明

`FormView` 是新的唯讀查詢模組，用來查詢 SQL Server `View`。

這個模組的設計目標很單純：

- 前端可先在 Designer 建立一個 View 查詢設定
- Runtime 只負責查詢與讀取單筆資料
- 不提供新增、修改、刪除資料的 API

和一般 `Form` 模組不同，`FormView` 不會寫回資料表，也不依賴 `BASE_TABLE_ID`，而是直接使用 `VIEW_TABLE_ID` / `VIEW_TABLE_NAME`。

## 適用情境

適合用在以下場景：

- 報表型查詢頁
- 彙總 View 查詢頁
- 只允許搜尋與瀏覽，不允許編輯的資料頁
- 已經有 SQL View，想快速接成系統查詢畫面

不適合用在以下場景：

- 需要 submit 儲存資料
- 需要 delete / delete-guard
- 需要 Master-Detail 寫入
- 需要 TVF 參數查詢

## API 一覽

### Designer API

| Method | Path | 用途 |
| --- | --- | --- |
| `GET` | `/Form/FormViewDesigner` | 取得 View 查詢主檔清單 |
| `GET` | `/Form/FormViewDesigner/{id}` | 取得某一筆 Designer 資料 |
| `DELETE` | `/Form/FormViewDesigner/{id}` | 刪除主檔 |
| `PUT` | `/Form/FormViewDesigner/form-name` | 更新表單名稱 |
| `GET` | `/Form/FormViewDesigner/tables/tableName?tableName=xxx` | 搜尋可用 View 名稱 |
| `GET` | `/Form/FormViewDesigner/tables/{viewName}/fields?formMasterId=...` | 同步並取得 View 欄位設定 |
| `GET` | `/Form/FormViewDesigner/fields/{fieldId}` | 取得單一欄位設定 |
| `POST` | `/Form/FormViewDesigner/fields` | 新增或更新欄位設定 |
| `POST` | `/Form/FormViewDesigner/fields/move` | 調整欄位順序 |
| `POST` | `/Form/FormViewDesigner/headers` | 建立或更新 View 查詢主檔 |

### Runtime API

| Method | Path | 用途 |
| --- | --- | --- |
| `GET` | `/Form/FormView/masters` | 取得可查詢的 View 設定清單 |
| `POST` | `/Form/FormView/search` | 依條件查詢 View 資料 |
| `POST` | `/Form/FormView/{formId}?pk=...` | 讀取單筆 View 資料 |

### `search` 與 `get form` 的差異

| API | 用途 | 輸入 | 輸出 | 適合場景 |
| --- | --- | --- | --- | --- |
| `/Form/FormView/search` | 查多筆 | `FormSearchRequest` | `List<FormListResponseViewModel>` | 列表頁、查詢頁、表格結果 |
| `/Form/FormView/{formId}?pk=...` | 查單筆 | `formId` + `pk` | `FormSubmissionViewModel` | 詳細頁、點列表某列後看單筆內容 |

補充：

- 如果前端只有查詢列表，通常只用 `search` 就夠了
- 如果前端還有詳細頁或既有單筆表單 UI，保留 `get form` 會比較方便
- `search` 查的是 View 的資料內容
- `get form` 查的是某一筆資料的完整欄位值
- 這兩支都不是拿來查欄位結構 metadata

### 查資料欄位 vs 查欄位結構

這三種需求要分開看：

| 需求 | API |
| --- | --- |
| 查 View 的資料內容 | `/Form/FormView/search` |
| 查某一筆 View 資料 | `/Form/FormView/{formId}?pk=...` |
| 查 View 欄位結構與欄位設定 | `/Form/FormViewDesigner/tables/{viewName}/fields` |

如果你要做的是：

- 動態組查詢欄位 UI
- 知道哪些欄位可查、顯示名稱是什麼、控制項類型是什麼

要用的是 Designer API 的欄位同步 endpoint，不是 Runtime 的 `search`。

## Designer 串接流程

前端通常會照下面順序做：

1. 呼叫 `GET /Form/FormViewDesigner/tables/tableName`
2. 讓使用者挑選一個 View
3. 呼叫 `POST /Form/FormViewDesigner/headers` 建立主檔
4. 呼叫 `GET /Form/FormViewDesigner/tables/{viewName}/fields?formMasterId=...` 同步欄位
5. 依需求呼叫 `POST /Form/FormViewDesigner/fields` 調整顯示欄位、查詢欄位、控制項型別
6. 最後在 Runtime 用 `/Form/FormView/search` 查詢

## 1. 搜尋可用 View

```http
GET /Form/FormViewDesigner/tables/tableName?tableName=vw_eqp
```

回傳範例：

```json
[
  "dbo.vw_eqp_status",
  "dbo.vw_eqp_alarm_summary"
]
```

說明：

- 只會查詢 View，不會回傳 Table
- `viewName` 可接受 `schema.object` 或單純 `object`
- 若只傳 `object`，系統預設以 `dbo` 處理
- 名稱只允許英數字與底線，避免非法 SQL identifier

## 2. 建立或更新 View 主檔

```http
POST /Form/FormViewDesigner/headers
Content-Type: application/json
```

Request 範例：

```json
{
  "ID": "00000000-0000-0000-0000-000000000000",
  "FORM_NAME": "設備狀態查詢",
  "FORM_CODE": "EQP_STATUS_VIEW",
  "FORM_DESCRIPTION": "設備狀態唯讀查詢畫面",
  "VIEW_TABLE_ID": "7f3fdfc7-03e5-4b54-b9df-5ad650d2a9ea"
}
```

欄位說明：

| 欄位 | 必填 | 說明 |
| --- | --- | --- |
| `ID` | 否 | 新增時可傳空 Guid；更新時帶既有主檔 ID |
| `FORM_NAME` | 否 | 前端顯示名稱 |
| `FORM_CODE` | 否 | 表單代碼 |
| `FORM_DESCRIPTION` | 否 | 補充說明 |
| `VIEW_TABLE_ID` | 是 | 必須是既有 View 物件的 master ID |

回傳範例：

```json
{
  "id": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2"
}
```

注意：

- 相同 `VIEW_TABLE_ID` 不可建立多筆 `FormView` 主檔
- `FUNCTION_TYPE` 會固定存成 `ViewQueryMaintenance`
- `SCHEMA_TYPE` 會固定存成 `OnlyView`
- `BASE_TABLE_ID` / `DETAIL_TABLE_ID` / `MAPPING_TABLE_ID` / `TVF_TABLE_ID` 都會是 `null`

## 3. 同步並取得 View 欄位

```http
GET /Form/FormViewDesigner/tables/dbo.vw_eqp_status/fields?formMasterId=f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2
```

回傳範例：

```json
{
  "Fields": [
    {
      "ID": "f0d7bf4a-c3b9-48a8-8c70-7e983d9bdc35",
      "FORM_FIELD_MASTER_ID": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2",
      "FORM_FIELD_DROPDOWN_ID": null,
      "TableName": "dbo.vw_eqp_status",
      "COLUMN_NAME": "EQP_NO",
      "DISPLAY_NAME": "設備編號",
      "DATA_TYPE": "nvarchar",
      "CONTROL_TYPE": 0,
      "IS_REQUIRED": false,
      "IS_EDITABLE": false,
      "IS_DISPLAYED": true,
      "IS_PK": true,
      "FIELD_ORDER": 1,
      "QUERY_COMPONENT": 1,
      "QUERY_CONDITION": 1,
      "CAN_QUERY": true,
      "SchemaType": 4
    }
  ]
}
```

這支 API 會做兩件事：

1. 依目前 View schema 補齊缺少的 `FORM_FIELD_CONFIG`
2. 回傳這個 View 對應的欄位設定

欄位預設規則：

- `IS_EDITABLE = false`
- `IS_REQUIRED = false`
- `CAN_QUERY` 依既有 schema 初始化邏輯決定
- `ViewFields` 會有值，`BaseFields` / `DetailFields` / `MappingFields` / `TvfFields` 會是空集合

## 4. 取得 Designer 主檔

```http
GET /Form/FormViewDesigner/f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2
```

回傳重點：

- `FormHeader` 會是這筆 View 查詢主檔
- `ViewFields` 會是欄位清單
- 其他欄位集合會是空的

前端可以直接把這份資料餵進現有 Designer UI，只是只處理 `ViewFields` 即可。

## 5. 更新欄位設定

```http
POST /Form/FormViewDesigner/fields
Content-Type: application/json
```

Request 範例：

```json
{
  "ID": "f0d7bf4a-c3b9-48a8-8c70-7e983d9bdc35",
  "FORM_FIELD_MASTER_ID": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2",
  "TableName": "dbo.vw_eqp_status",
  "COLUMN_NAME": "EQP_NO",
  "DISPLAY_NAME": "設備編號",
  "CONTROL_TYPE": 0,
  "IS_DISPLAYED": true,
  "CAN_QUERY": true,
  "QUERY_COMPONENT": 1,
  "QUERY_CONDITION": 1
}
```

注意：

- `SchemaType` 會在後端固定成 `OnlyView`
- `IS_EDITABLE` / `IS_REQUIRED` 會被強制維持 `false`
- 若 `CAN_QUERY = false`，就不應再設定查詢元件

## 6. 取得 Runtime masters

```http
GET /Form/FormView/masters
```

回傳範例：

```json
[
  {
    "Id": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2",
    "FormName": "設備狀態查詢",
    "ViewTableId": "7f3fdfc7-03e5-4b54-b9df-5ad650d2a9ea",
    "ViewTableName": "dbo.vw_eqp_status"
  }
]
```

欄位說明：

| 欄位 | 說明 |
| --- | --- |
| `Id` | `FormView` 主檔 ID，查詢時要帶進 `FormMasterId` |
| `FormName` | 前端顯示名稱 |
| `ViewTableId` | 這筆設定綁定的 View master ID |
| `ViewTableName` | 實際 SQL View 名稱 |

## 7. 查詢 View 資料

```http
POST /Form/FormView/search
Content-Type: application/json
```

Request 範例：

```json
{
  "FormMasterId": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2",
  "Page": 1,
  "PageSize": 20,
  "Conditions": [
    {
      "Column": "EQP_NO",
      "ConditionType": 1,
      "Value": "EQP-001"
    },
    {
      "Column": "REPORT_TIME",
      "ConditionType": 3,
      "Value": "2026-04-01 00:00:00",
      "Value2": "2026-04-30 23:59:59"
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

Request 欄位說明：

| 欄位 | 必填 | 說明 |
| --- | --- | --- |
| `FormMasterId` | 否 | 指定查某一個 View 設定；不帶時會查全部 `FormView` masters |
| `Page` | 否 | 第幾頁，最小會被視為 `1` |
| `PageSize` | 否 | 每頁筆數，最小會被視為 `1` |
| `Conditions` | 否 | 查詢條件 |
| `OrderBys` | 否 | 排序條件 |

補充：

- `search` 可以搜尋 View 的欄位資料
- 查詢欄位名稱請放在 `Conditions[].Column`
- 例如 `EQP_NO`、`STATUS`、`REPORT_TIME`
- 後端會將這些條件轉成對 View 的 `WHERE` 查詢
- 這支 API 查的是資料，不是 schema

## Conditions

範例：

```json
[
  {
    "Column": "STATUS",
    "ConditionType": 1,
    "Value": "RUN"
  },
  {
    "Column": "REPORT_TIME",
    "ConditionType": 3,
    "Value": "2026-04-01 00:00:00",
    "Value2": "2026-04-30 23:59:59"
  },
  {
    "Column": "EQP_TYPE",
    "ConditionType": 8,
    "Values": ["ETCH", "CVD"]
  }
]
```

`ConditionType` 對照：

| 值 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `None` | 不套條件 |
| `1` | `Equal` | 等於 |
| `2` | `Like` | 模糊查詢，後端會包成 `%value%` |
| `3` | `Between` | `Value` 到 `Value2` |
| `4` | `GreaterThan` | 大於 |
| `5` | `GreaterThanOrEqual` | 大於等於 |
| `6` | `LessThan` | 小於 |
| `7` | `LessThanOrEqual` | 小於等於 |
| `8` | `In` | 使用 `Values` |
| `9` | `NotEqual` | 不等於 |
| `10` | `NotIn` | 使用 `Values` 的排除條件 |

注意：

- `Column` 必須是 View 中實際存在的欄位名
- `Column` 只允許英數字與底線
- 不合法欄位名會被忽略或拒絕，不會直接拼接成危險 SQL

## OrderBys

範例：

```json
[
  {
    "Column": "REPORT_TIME",
    "Direction": 1
  }
]
```

`Direction` 對照：

| 值 | 名稱 | 說明 |
| --- | --- | --- |
| `0` | `Asc` | 升冪 |
| `1` | `Desc` | 降冪 |

## 查詢回傳格式

`POST /Form/FormView/search` 回傳的是 `List<FormListResponseViewModel>`。

範例：

```json
[
  {
    "FormMasterId": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2",
    "FormName": "設備狀態查詢",
    "BaseId": "7f3fdfc7-03e5-4b54-b9df-5ad650d2a9ea",
    "TotalPageSize": 125,
    "Items": [
      {
        "Pk": "EQP-001",
        "Fields": [
          {
            "FieldConfigId": "f0d7bf4a-c3b9-48a8-8c70-7e983d9bdc35",
            "Column": "EQP_NO",
            "DISPLAY_NAME": "設備編號",
            "DATA_TYPE": "nvarchar",
            "CONTROL_TYPE": 0,
            "IS_REQUIRED": false,
            "IS_EDITABLE": false,
            "IS_DISPLAYED": true,
            "IS_PK": true,
            "QUERY_COMPONENT": 1,
            "QUERY_CONDITION": 1,
            "CAN_QUERY": true,
            "OptionList": [],
            "CurrentValue": "EQP-001"
          },
          {
            "FieldConfigId": "bb2750e0-4f78-4d91-b7aa-5164f1fbacbd",
            "Column": "STATUS",
            "DISPLAY_NAME": "狀態",
            "DATA_TYPE": "nvarchar",
            "CONTROL_TYPE": 4,
            "IS_REQUIRED": false,
            "IS_EDITABLE": false,
            "IS_DISPLAYED": true,
            "IS_PK": false,
            "QUERY_COMPONENT": 4,
            "QUERY_CONDITION": 1,
            "CAN_QUERY": true,
            "OptionList": [
              {
                "OPTION_TEXT": "運轉中",
                "OPTION_VALUE": "RUN"
              }
            ],
            "CurrentValue": "運轉中"
          }
        ]
      }
    ]
  }
]
```

回傳重點：

- 最外層是 array
- 每個 array item 代表一個 `FormMasterId`
- `Items` 才是真正的資料列
- 每列資料仍然是 `Fields` 陣列，不是固定欄位 object
- 若欄位有 dropdown 設定，`CurrentValue` 會盡量轉成顯示文字

## 前端把查詢結果轉成表格

通常可以這樣處理欄位：

```ts
const response = await api.post("/Form/FormView/search", request);
const formResult = response.data[0];

const columns =
  formResult?.Items?.[0]?.Fields
    ?.filter((field: any) => field.IS_DISPLAYED)
    .map((field: any) => ({
      key: field.Column,
      title: field.DISPLAY_NAME || field.Column
    })) ?? [];

const rows =
  formResult?.Items?.map((item: any) => {
    const row: Record<string, unknown> = {
      pk: item.Pk
    };

    item.Fields
      .filter((field: any) => field.IS_DISPLAYED)
      .forEach((field: any) => {
        row[field.Column] = field.CurrentValue;
      });

    return row;
  }) ?? [];
```

## 8. 讀取單筆 View 資料

```http
POST /Form/FormView/f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2?pk=EQP-001
```

回傳的是 `FormSubmissionViewModel`。

範例：

```json
{
  "FormId": "f3d5c9bf-4bc6-4bd8-b2c7-721d2ac0d1c2",
  "Pk": "EQP-001",
  "TargetTableToUpsert": "dbo.vw_eqp_status",
  "FormName": "設備狀態查詢",
  "Fields": [
    {
      "FieldConfigId": "f0d7bf4a-c3b9-48a8-8c70-7e983d9bdc35",
      "Column": "EQP_NO",
      "DISPLAY_NAME": "設備編號",
      "CurrentValue": "EQP-001",
      "IS_EDITABLE": false,
      "IS_REQUIRED": false
    }
  ]
}
```

注意：

- 這支 API 是唯讀用途，不是 submit
- `pk` 會用來查 View 的主鍵欄位
- 若該 View 沒有主鍵，單筆讀取可能失敗
- 若 View 是複合主鍵，`FormView` 目前不支援

## 查詢元件建議

前端可以依 `Fields` 中的設定動態生成查詢 UI：

- `CAN_QUERY = true` 的欄位才顯示在查詢區
- 依 `QUERY_COMPONENT` 決定渲染元件
- 依 `QUERY_CONDITION` 組出 `Conditions`

常見對照：

| `QUERY_COMPONENT` | 建議元件 |
| --- | --- |
| `0` | 不顯示查詢元件 |
| `1` | 文字輸入框 |
| `2` | 數字輸入框 |
| `3` | 日期元件 |
| `4` | 下拉選單 |
| `5` | 數值比較元件 |
| `6` | 日期區間元件 |

## 和一般 Form 的差異

| 項目 | FormView | 一般 Form |
| --- | --- | --- |
| 資料來源 | SQL View | Table / Master-Detail / Mapping / TVF |
| 可否 submit | 否 | 是 |
| 可否 delete | 否 | 視模組而定 |
| 查詢主體 | `VIEW_TABLE_NAME` | `BASE_TABLE_NAME` 或其他來源 |
| 頁面定位 | 查詢 / 瀏覽 | CRUD |

## 前端實作建議

### 查詢頁

建議流程：

1. 頁面載入先呼叫 `/Form/FormView/masters`
2. 使用者選擇一個 View 查詢設定
3. 根據這個設定的欄位描述建立查詢表單
4. 呼叫 `/Form/FormView/search`
5. 把 `Items[].Fields[]` 攤平成表格列

### 詳細頁

若清單列有 `Pk`，可以在點擊列時：

1. 帶 `FormMasterId`
2. 帶 `Pk`
3. 呼叫 `POST /Form/FormView/{formId}?pk=...`
4. 使用 `Fields` 渲染唯讀詳細頁

## 限制與注意事項

- 這個模組只支援唯讀
- View 名稱只接受安全格式的 identifier
- 不支援複合主鍵的單筆讀取
- 若 View 沒有主鍵，清單 `Pk` 可能為 `null`
- 分頁時若前端沒傳 `OrderBys`，後端會優先使用主鍵排序；沒有主鍵時才退回 `ORDER BY (SELECT NULL)`
- Runtime 雖然會處理 dropdown 顯示文字，但這份文件只描述目前 `FormView` 已開放的主要 API

## 最小串接範例

```ts
const masters = await api.get("/Form/FormView/masters");
const selected = masters.data[0];

const result = await api.post("/Form/FormView/search", {
  FormMasterId: selected.Id,
  Page: 1,
  PageSize: 20,
  Conditions: [
    {
      Column: "EQP_NO",
      ConditionType: 1,
      Value: "EQP-001"
    }
  ]
});

const rows = result.data[0]?.Items ?? [];
```

以上流程就能完成一個最基本的 `FormView` 查詢頁。
