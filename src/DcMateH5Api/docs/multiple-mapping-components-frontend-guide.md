# Multiple Mapping 逐 SID 動態元件－前端串接指南

## 1. 這個功能在做什麼？

同一張 Mapping Table 的每一筆資料，都可以設定不同的輸入元件。

例如：

- SID `501` 使用 Dropdown，值只能選 `A` 或 `D`。
- SID `502` 使用 Text，值由使用者自由輸入。

後端不會寫死欄位名稱一定叫 `SID`。表單 Header 的 `MAPPING_PK_COLUMN` 是哪個欄位，該欄位的值就是 API 對外使用的 `MappingRowId`。

所有元件的值都寫入 Header 設定的 `TARGET_MAPPING_COLUMN_NAME`。

## 2. 前端最短流程

### Designer 設定元件

1. 查詢已建立的 Mapping Rows。
2. 從查詢結果取得 `MappingRowId`。
3. 替該 `MappingRowId` 設定 Text、Dropdown、Radio 等元件。

### Runtime 顯示與更新

1. 查詢 `items/query`。
2. 從 `ComponentsByMappingRowId` 取得每筆 Mapping Row 的元件。
3. `IsConfigured=true` 才顯示可輸入元件。
4. 使用者輸入後，呼叫逐 SID Value API 更新值。

```mermaid
flowchart LR
    A["建立關聯"] --> B["重新查詢取得 MappingRowId"]
    B --> C["Designer 設定元件"]
    C --> D["Runtime 依 ControlType 顯示"]
    D --> E["呼叫 Value API 更新值"]
```

## 3. 最重要的欄位

| 欄位 | 意義 | 前端用途 |
|---|---|---|
| `BaseId` | Base Table 的主鍵值 | 指定要查看哪一筆主資料的關聯 |
| `DetailPk` | Detail Table 的主鍵值 | `Linked`／`Unlinked` Dictionary 的 key |
| `MappingRowId` | Mapping Table 的識別值，通常就是 SID | `ComponentsByMappingRowId` 的 key，也是設定及更新元件時使用的 ID |
| `IsConfigured` | 該 Mapping Row 是否已設定元件 | `false` 時不要顯示輸入元件 |
| `CurrentValue` | `TARGET_MAPPING_COLUMN_NAME` 目前的值 | Runtime 顯示的初始值 |
| `ControlType` | 要顯示的元件類型 | 決定前端要產生哪種 UI |
| `Options` | Dropdown／Radio 的有效選項 | 顯示 `Text`，送出 `Value` |

`DetailPk` 和 `MappingRowId` 不可混用：

```ts
const linkedRow = response.Linked[detailPk];
const component = response.ComponentsByMappingRowId[linkedRow.MappingRowId];
```

Unlinked 資料尚未建立 Mapping Row，因此沒有可用的 `MappingRowId`，也不會出現在 `ComponentsByMappingRowId`。

## 4. API 一覽

| 用途 | Method | 路徑 |
|---|---|---|
| Designer 查詢元件設定 | `POST` | `/Form/FormDesignerMultipleMapping/{formMasterId}/mapping-components/query` |
| Designer 新增或覆寫元件 | `PUT` | `/Form/FormDesignerMultipleMapping/{formMasterId}/mapping-components/{mappingRowId}` |
| Designer 清除元件 | `DELETE` | `/Form/FormDesignerMultipleMapping/{formMasterId}/mapping-components/{mappingRowId}` |
| Runtime 查詢關聯與元件 | `POST` | `/Form/FormMultipleMapping/{formMasterId}/items/query` |
| Runtime 更新元件值 | `PUT` | `/Form/FormMultipleMapping/{formMasterId}/mapping-components/{mappingRowId}/value` |

路徑中的 `mappingRowId` 請先編碼：

```ts
const encodedMappingRowId = encodeURIComponent(mappingRowId);
```

## 5. Enum 與基本型別

API 使用數字 Enum，JSON 欄位名稱使用 PascalCase。

```ts
export enum FormControlType {
  None = 0,
  Text = 1,
  Number = 2,
  Date = 3,
  Checkbox = 4,
  Textarea = 5,
  Dropdown = 6,
  DateTime = 7,
  Radio = 8,
}

export enum MappingListType {
  All = 0,
  LinkedOnly = 1,
  UnlinkedOnly = 2,
}

export interface MappingComponentOption {
  Value: string;
  Text: string;
  Order: number;
}

export interface RuntimeMappingComponent {
  MappingRowId: string;
  DetailPk: string;
  ControlType: FormControlType;
  CurrentValue: unknown;
  Options: MappingComponentOption[];
  IsConfigured: boolean;
}
```

## 6. 查詢 Request 怎麼填？

Designer 查詢和 Runtime 查詢共用 `MappingListQuery`。

### 6.1 最簡單的 Designer 查詢

Designer 固定只查已關聯的 Mapping Rows，所以不用傳 `Type`：

```json
{
  "BaseId": "1001",
  "Page": 1,
  "PageSize": 20,
  "OrderBySeqAscending": true
}
```

### 6.2 最簡單的 Runtime 查詢

只需要顯示已關聯資料及元件時，使用 `LinkedOnly`：

```json
{
  "BaseId": "1001",
  "Type": 1,
  "Page": 1,
  "PageSize": 20,
  "OrderBySeqAscending": true
}
```

若畫面同時需要左右兩側的 Linked／Unlinked 清單，將 `Type` 改成 `0`。

規則：

- `BaseId` 必填。
- `Page`、`PageSize` 必須一起傳，或一起省略。
- `Page`、`PageSize` 必須大於 `0`。
- Runtime 的 `Type` 請明確傳入。

### 6.3 DetailConditions 與 MappingConditions

這兩個欄位只是「額外篩選條件」，不需要篩選時可以省略或傳空陣列。

| 欄位 | 查哪張表 | 例子 |
|---|---|---|
| `DetailConditions` | Detail Table | 用料號、名稱或狀態篩選明細資料 |
| `MappingConditions` | Mapping Table | 用 Mapping 的 SEQ、建立時間或其他欄位篩選已關聯資料 |

範例：Detail 的 `NAME` 包含「馬達」，且 Mapping 的 `SEQ` 大於等於 `10`：

```json
{
  "BaseId": "1001",
  "Type": 1,
  "DetailConditions": [
    {
      "Column": "NAME",
      "ConditionType": 2,
      "Value": "馬達"
    }
  ],
  "MappingConditions": [
    {
      "Column": "SEQ",
      "ConditionType": 5,
      "Value": "10"
    }
  ],
  "Page": 1,
  "PageSize": 20,
  "OrderBySeqAscending": true
}
```

常用的 `ConditionType`：

| 數值 | 意義 | 使用欄位 |
|---:|---|---|
| `1` | 等於 | `Value` |
| `2` | 包含／模糊查詢 | `Value` |
| `3` | 區間 | `Value`、`Value2` |
| `5` | 大於等於 | `Value` |
| `8` | IN | `Values` |
| `9` | 不等於 | `Value` |
| `10` | NOT IN | `Values` |
| `11` | IS NULL | 不用傳值 |
| `12` | IS NOT NULL | 不用傳值 |

注意：

- `Column` 必須是對應資料表的真實欄位名稱。
- 此 API 會從資料庫 Schema 取得型別，前端不需要自行判斷 `DataType`。
- Unlinked 資料沒有 Mapping Row，因此 `Type=2` 時不可傳 `MappingConditions`。

## 7. Designer：設定每筆 SID 的元件

後端會依 Target Column 的 SQL 型別檢查可用的 `ControlType`。若設定不相容的元件，API 會回傳 `400`。

要取消元件設定時請呼叫 DELETE，不要用 PUT 傳入 `ControlType=0`。

### 7.1 查詢目前設定

```http
POST /Form/FormDesignerMultipleMapping/{formMasterId}/mapping-components/query
Content-Type: application/json
```

Response 範例：

```json
{
  "FormMasterId": "9b31e6b1-5b3f-4ef2-beb0-63954e3d21aa",
  "TotalCount": 2,
  "ComponentsByMappingRowId": {
    "501": {
      "MappingRowId": "501",
      "DetailPk": "2001",
      "ControlType": 6,
      "CurrentValue": "A",
      "IsUseSql": false,
      "DropdownSql": null,
      "Options": [
        { "Value": "A", "Text": "啟用", "Order": 1 },
        { "Value": "D", "Text": "停用", "Order": 2 }
      ],
      "IsConfigured": true
    },
    "502": {
      "MappingRowId": "502",
      "DetailPk": "2002",
      "ControlType": 0,
      "CurrentValue": null,
      "IsUseSql": false,
      "DropdownSql": null,
      "Options": [],
      "IsConfigured": false
    }
  }
}
```

### 7.2 設定 Text

```http
PUT /Form/FormDesignerMultipleMapping/{formMasterId}/mapping-components/{mappingRowId}
Content-Type: application/json
```

```json
{
  "ControlType": 1,
  "IsUseSql": false,
  "DropdownSql": null,
  "Options": []
}
```

### 7.3 設定靜態 Dropdown

```json
{
  "ControlType": 6,
  "IsUseSql": false,
  "DropdownSql": null,
  "Options": [
    { "Value": "A", "Text": "啟用", "Order": 1 },
    { "Value": "D", "Text": "停用", "Order": 2 }
  ]
}
```

Radio 使用相同格式，只要把 `ControlType` 改成 `8`。

### 7.4 設定 SQL Dropdown

```json
{
  "ControlType": 6,
  "IsUseSql": true,
  "DropdownSql": "SELECT STATUS_CODE AS ID, STATUS_NAME AS NAME FROM ADM_STATUS",
  "Options": []
}
```

SQL 必須符合以下規則：

- 只能有一個唯讀 `SELECT`。
- 結果必須包含 `ID`、`NAME` 欄位。
- `ID`、`NAME` 不可為 null 或空字串。
- `ID` 不可重複，而且必須能轉成 Target Column 的 SQL 型別。

儲存時，後端會執行 SQL 並保存選項快照。Runtime 查詢不會每次重新執行 Dropdown SQL。

### 7.5 設定成功與清除設定

新增、覆寫及刪除成功都回傳 `204 No Content`。

清除元件：

```http
DELETE /Form/FormDesignerMultipleMapping/{formMasterId}/mapping-components/{mappingRowId}
```

清除後 Mapping Row 還在，但會變成：

```json
{
  "ControlType": 0,
  "Options": [],
  "IsConfigured": false
}
```

## 8. Runtime：顯示與更新元件

### 8.1 查詢 Runtime 資料

```http
POST /Form/FormMultipleMapping/{formMasterId}/items/query
Content-Type: application/json
```

Response 會保留原本的 `Linked`、`Unlinked`，並加入 `ComponentsByMappingRowId`：

```json
{
  "TargetMappingColumnName": "STATUS_CODE",
  "Linked": {
    "2001": {
      "MappingRowId": "501",
      "DetailPk": "2001",
      "MappingFields": {},
      "DetailFields": {}
    }
  },
  "Unlinked": {},
  "ComponentsByMappingRowId": {
    "501": {
      "MappingRowId": "501",
      "DetailPk": "2001",
      "ControlType": 6,
      "CurrentValue": "A",
      "Options": [
        { "Value": "A", "Text": "啟用", "Order": 1 },
        { "Value": "D", "Text": "停用", "Order": 2 }
      ],
      "IsConfigured": true
    }
  }
}
```

### 8.2 產生 UI

```ts
function getInputKind(component: RuntimeMappingComponent): string {
  if (!component.IsConfigured || component.ControlType === FormControlType.None) {
    return "none";
  }

  switch (component.ControlType) {
    case FormControlType.Text: return "text";
    case FormControlType.Number: return "number";
    case FormControlType.Date: return "date";
    case FormControlType.Checkbox: return "checkbox";
    case FormControlType.Textarea: return "textarea";
    case FormControlType.Dropdown: return "select";
    case FormControlType.DateTime: return "datetime-local";
    case FormControlType.Radio: return "radio";
    default: return "none";
  }
}
```

Dropdown／Radio：

- 畫面顯示 `Options[].Text`。
- 送出時使用 `Options[].Value`。
- 不要自行產生 Options 以外的值。

### 8.3 更新值

```http
PUT /Form/FormMultipleMapping/{formMasterId}/mapping-components/{mappingRowId}/value
Content-Type: application/json
```

```json
{
  "Value": "A"
}
```

成功時：

```json
{
  "Affected": 1
}
```

後端會檢查：

- 該 `MappingRowId` 必須已設定元件。
- Dropdown／Radio 不接受 `null`。
- Dropdown／Radio 的值必須存在於有效 `Options`。
- 所有輸入都必須能轉成 Target Column 的 SQL 型別。

Runtime 更新 `TARGET_MAPPING_COLUMN_NAME` 時，請使用這支 Value API。

既有的 `/mapping-table` API 仍可更新其他 Mapping 欄位；若用它更新 Target Column，也會套用相同的元件與選項驗證，不能用來繞過限制。

### 8.4 Fetch 範例

```ts
export async function updateMappingComponentValue(
  formMasterId: string,
  mappingRowId: string,
  value: unknown,
): Promise<{ Affected: number }> {
  const response = await fetch(
    `/Form/FormMultipleMapping/${formMasterId}` +
      `/mapping-components/${encodeURIComponent(mappingRowId)}/value`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ Value: value }),
    },
  );

  if (!response.ok) {
    throw new Error(await readApiError(response));
  }

  return response.json();
}
```

## 9. 新增與移除關聯

### 新增關聯

Unlinked Row 沒有 `MappingRowId`，請依照以下順序：

1. 呼叫 `POST /Form/FormMultipleMapping/{formMasterId}/items` 建立關聯。
2. 重新呼叫 `items/query`。
3. 從 `Linked[detailPk].MappingRowId` 取得新產生的 Mapping Row ID。
4. 再呼叫 Designer API 設定元件。

剛建立的 Mapping Row 預設為 `IsConfigured=false`，Runtime 不應顯示可輸入元件。

### 移除關聯

呼叫：

```http
POST /Form/FormMultipleMapping/{formMasterId}/items/remove
```

後端會在同一交易中移除 Mapping Row，並軟刪除對應的元件及選項。前端成功後重新查詢即可。

## 10. 錯誤處理

| HTTP 狀態 | 意義 | 前端處理 |
|---|---|---|
| `200` | 查詢或 Runtime 更新成功 | 更新畫面資料 |
| `204` | Designer 設定、清除或關聯操作成功 | 重新查詢 |
| `400` | 請求、選項、SQL 或型別驗證失敗 | 顯示後端訊息，不更新本機值 |
| `404` | 表單或 Mapping Row 不存在 | 重新載入或提示資料已移除 |
| `409` | 尚有逐 SID 設定，不能更換 Mapping Table／PK／Target Column | 提示先清除逐 SID 設定 |

部分錯誤是純文字，部分是 JSON。可使用：

```ts
async function readApiError(response: Response): Promise<string> {
  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    const body = await response.json();
    return body.detail ?? body.title ?? JSON.stringify(body);
  }

  return response.text();
}
```

## 11. 前端檢查清單

- [ ] 使用 PascalCase 讀取 API 欄位。
- [ ] `Linked`／`Unlinked` 使用 `DetailPk` 當 key。
- [ ] `ComponentsByMappingRowId` 使用 Mapping PK／SID 當 key。
- [ ] `IsConfigured=false` 時不顯示輸入元件。
- [ ] Dropdown／Radio 顯示 `Text`，送出 `Value`。
- [ ] Runtime 更新 Target Column 時呼叫逐 SID Value API。
- [ ] `mappingRowId` 放入 URL 前使用 `encodeURIComponent`。
- [ ] 新增關聯後重新查詢，不能把 `DetailPk` 當成 `MappingRowId`。
- [ ] API 回 400 時保留原值並顯示訊息。

## 12. 後端部署提醒

啟用功能前必須執行：

```text
src/DcMateH5Api/docs/sql/20260720-multiple-mapping-component.sql
```

設定資料表名稱：

- `FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_CONFIG`
- `FORM_FIELD_MULTIPLE_MAPPING_COMPONENT_OPTION`

表單 Header 必須設定：

- `MAPPING_TABLE_NAME`
- `MAPPING_PK_COLUMN`
- `MAPPING_BASE_FK_COLUMN`
- `MAPPING_DETAIL_FK_COLUMN`
- `TARGET_MAPPING_COLUMN_NAME`
