# MmsLotController 前端串接文件

本文件描述 `MmsLotController` 提供的 MLOT（物料批）操作 API。

## API 概覽

Base path：

```http
/api/MMS/MmsLot
```

所有 API：

- 使用 `POST`
- Request body 使用 JSON
- 成功與業務錯誤皆回傳 `Result<boolean>`
- JSON 欄位名稱保留大寫與底線，前端請依文件送出

| API | Endpoint | 用途 |
| --- | --- | --- |
| 建立物料批 | `/CreateMLot` | 建立 MLOT，初始狀態固定為 `Wait` |
| 消耗物料批 | `/MLotConsume` | 指定 LOT 消耗 MLOT 庫存 |
| 取消消耗 | `/MLotUNConsume` | 將數量加回 MLOT，移除 LOT 與 MLOT 使用關係 |
| 變更狀態 | `/MLotStateChange` | 手動變更 MLOT 狀態，不異動數量 |

完整 URL 範例：

```http
POST /api/MMS/MmsLot/CreateMLot
```

## 共用回傳格式

TypeScript 型別：

```ts
export interface ApiResult<T> {
  IsSuccess: boolean;
  Code: string;
  Message: string;
  Data: T;
  ErrorData: unknown | null;
}
```

成功回傳，HTTP `200`：

```json
{
  "IsSuccess": true,
  "Data": true,
  "Code": "",
  "Message": "",
  "ErrorData": null
}
```

失敗回傳：

```json
{
  "IsSuccess": false,
  "Data": false,
  "Code": "BadRequest",
  "Message": "MLOT not found: MLOT-001",
  "ErrorData": null
}
```

| HTTP status | `Code` | 說明 |
| --- | --- | --- |
| `200` | 空字串 | 操作成功 |
| `400` | `BadRequest` | 欄位驗證失敗、使用者／LOT／MLOT／狀態不存在，或庫存不足 |
| `409` | `Conflict` | MLOT 已存在，或資料被其他操作同步修改 |
| `500` | `UnhandledException` | 未預期的伺服器錯誤 |

前端應同時檢查 HTTP status 與 `IsSuccess`，失敗時可直接顯示 `Message`。成功時 `Code` 與 `Message` 都是空字串。

## 共用欄位

| 欄位 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `DATA_LINK_SID` | `number` | 是 | 操作識別值，必須大於 `0` |
| `REPORT_TIME` | `string \| null` | 否 | ISO 8601 日期時間；未傳時使用資料庫目前時間 |
| `ACCOUNT_NO` | `string` | 是 | 操作者帳號，必須存在於系統 |
| `INPUT_FORM_NAME` | `string \| null` | 否 | 呼叫來源，例如頁面或表單名稱 |
| `COMMENT` | `string \| null` | 否 | 備註 |

建議日期格式：

```text
2026-06-15T14:30:00
```

## 1. CreateMLot

建立一筆 MLOT。建立後狀態固定為 `Wait`，前端不需要傳狀態。

```http
POST /api/MMS/MmsLot/CreateMLot
```

TypeScript request：

```ts
export interface CreateMLotRequest {
  DATA_LINK_SID: number;
  MLOT: string;
  PARENT_MLOT?: string | null;
  ALIAS_MLOT1?: string | null;
  ALIAS_MLOT2?: string | null;
  MLOT_TYPE?: string | null;
  PART_NO: string;
  MLOT_QTY: number;
  MLOT_WO?: string | null;
  EXPIRY_DATE?: string | null;
  DATE_CODE?: string | null;
  REPORT_TIME?: string | null;
  ACCOUNT_NO: string;
  INPUT_FORM_NAME?: string | null;
  COMMENT?: string | null;
}
```

必要規則：

- `DATA_LINK_SID > 0`
- `MLOT` 不可空白，且不可與既有 MLOT 重複
- `PART_NO` 不可空白
- `MLOT_QTY > 0`
- `ACCOUNT_NO` 不可空白，且帳號必須存在
- `PARENT_MLOT` 未填時，後端會使用 `MLOT`
- `MLOT_TYPE` 未填時，後端會使用 `N`

Request body 範例：

```json
[
  {
    "DATA_LINK_SID": 900000004001,
    "MLOT": "MLOT-001",
    "PARENT_MLOT": null,
    "ALIAS_MLOT1": "MLOT-001-A1",
    "ALIAS_MLOT2": null,
    "MLOT_TYPE": "N",
    "PART_NO": "PART-001",
    "MLOT_QTY": 10,
    "MLOT_WO": "WO-001",
    "EXPIRY_DATE": "2026-12-31T23:59:59",
    "DATE_CODE": "20260615",
    "REPORT_TIME": "2026-06-15T14:30:00",
    "ACCOUNT_NO": "TestAc1",
    "INPUT_FORM_NAME": "MmsLotCreate",
    "COMMENT": "create material lot"
  }
]
```

## 2. MLotConsume

由指定 LOT 消耗 MLOT 數量。

```http
POST /api/MMS/MmsLot/MLotConsume
```

TypeScript request：

```ts
export interface MLotConsumeRequest {
  DATA_LINK_SID: number;
  MLOT: string;
  LOT: string;
  CONSUME_QTY: number;
  REPORT_TIME?: string | null;
  ACCOUNT_NO: string;
  INPUT_FORM_NAME?: string | null;
  COMMENT?: string | null;
}
```

必要規則與結果：

- `DATA_LINK_SID > 0`
- `MLOT` 與 `LOT` 必須存在
- `CONSUME_QTY > 0`
- Backend reads `WIP_LOT.PART_NO -> WIP_PARTNO.PARTNO_CATEGORY` to decide stock deduction.
- `PARTNO_CATEGORY = L1`, blank, or `NULL`: `MLOT_QTY` deducts `CONSUME_QTY`; history `TRANSATION_QTY` records `-CONSUME_QTY`.
- `PARTNO_CATEGORY = L2`: `MLOT_QTY` is not deducted; history `TRANSATION_QTY` records `-CONSUME_QTY`.
- Other nonblank `PARTNO_CATEGORY` values return `400 BadRequest`.
- After the category-based deduction, remaining quantity `0` changes MLOT status to `Finished`; otherwise status is `Wait`.

Request body 範例：

```json
{
  "DATA_LINK_SID": 900000004002,
  "MLOT": "MLOT-001",
  "LOT": "LOT-001",
  "CONSUME_QTY": 4,
  "REPORT_TIME": "2026-06-15T14:35:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "MmsLotConsume",
  "COMMENT": "consume material"
}
```

## 3. MLotUNConsume

取消指定 LOT 對 MLOT 的消耗，將數量加回庫存。

```http
POST /api/MMS/MmsLot/MLotUNConsume
```

TypeScript request：

```ts
export interface MLotUNConsumeRequest {
  DATA_LINK_SID: number;
  LOT: string;
  MLOT: string;
  UNCONSUME_QTY: number;
  REPORT_TIME?: string | null;
  ACCOUNT_NO: string;
  INPUT_FORM_NAME?: string | null;
  COMMENT?: string | null;
}
```

必要規則與結果：

- `DATA_LINK_SID > 0`
- `MLOT` 與 `LOT` 必須存在
- `UNCONSUME_QTY > 0`
- `MLOT_QTY` 會增加 `UNCONSUME_QTY`
- 後端會移除指定 `LOT + MLOT` 的目前使用關係
- 此 API **不會自動變更 MLOT 狀態**；若原狀態是 `Finished`，加回數量後仍為 `Finished`
- 如需將狀態改回 `Wait`，必須再呼叫 `MLotStateChange`

Request body 範例：

```json
{
  "DATA_LINK_SID": 900000004003,
  "LOT": "LOT-001",
  "MLOT": "MLOT-001",
  "UNCONSUME_QTY": 4,
  "REPORT_TIME": "2026-06-15T14:40:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "MmsLotConsumeCancel",
  "COMMENT": "cancel material consumption"
}
```

## 4. MLotStateChange

手動變更 MLOT 狀態，不異動 MLOT 數量。

```http
POST /api/MMS/MmsLot/MLotStateChange
```

TypeScript request：

```ts
export interface MLotStateChangeRequest {
  DATA_LINK_SID: number;
  MLOT: string;
  NEW_MLOT_STATE_CODE: string;
  REASON_CODE?: string | null;
  REPORT_TIME?: string | null;
  ACCOUNT_NO: string;
  INPUT_FORM_NAME?: string | null;
  COMMENT?: string | null;
}
```

必要規則與結果：

- `DATA_LINK_SID > 0`
- `MLOT` 必須存在
- `NEW_MLOT_STATE_CODE` 不可空白，且必須存在於後端 MLOT 狀態資料
- `REASON_CODE` 選填；找不到對應原因時，後端仍會執行狀態變更，但不記錄原因
- 此 API 只改狀態，不改 `MLOT_QTY`

目前主要狀態：

| 狀態 | 說明 |
| --- | --- |
| `Wait` | 可用／等待使用 |
| `Finished` | 已完成 |

Request body 範例：

```json
{
  "DATA_LINK_SID": 900000004004,
  "MLOT": "MLOT-001",
  "NEW_MLOT_STATE_CODE": "Wait",
  "REASON_CODE": "1",
  "REPORT_TIME": "2026-06-15T14:45:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "MmsLotStateChange",
  "COMMENT": "reopen material lot"
}
```

## 前端呼叫範例

```ts
async function createMLot(
  payload: CreateMLotRequest[],
): Promise<ApiResult<boolean>> {
  const response = await fetch("/api/MMS/MmsLot/CreateMLot", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

  const result = (await response.json()) as ApiResult<boolean>;

  if (!response.ok || !result.IsSuccess) {
    throw new Error(result.Message ?? "MLOT operation failed");
  }

  return result;
}
```

## 常見錯誤訊息

| 訊息 | 原因 |
| --- | --- |
| `DATA_LINK_SID must be greater than 0.` | `DATA_LINK_SID` 未填或小於等於 `0` |
| `MLOT is required.` | `MLOT` 空白 |
| `LOT is required.` | `LOT` 空白 |
| `ACCOUNT_NO is required.` | `ACCOUNT_NO` 空白 |
| `MLOT already exists: ...` | 建立時 MLOT 編號重複 |
| `MLOT not found: ...` | 找不到指定 MLOT |
| `LOT not found: ...` | 找不到指定 LOT |
| `User not found: ...` | 找不到指定操作帳號 |
| `MLOT status not found: ...` | 找不到指定狀態 |
| `MLOT_QTY is insufficient: ...` | 消耗數量超過目前庫存 |
| `Unsupported WIP_PARTNO.PARTNO_CATEGORY for MLotConsume: ...` | `WIP_PARTNO.PARTNO_CATEGORY` 不是 `L1`、`L2` 或空值 |

## 前端實作注意事項

- 數量欄位必須送數字，不要送字串。
- 日期欄位建議送 ISO 8601 字串；未填可省略或送 `null`。
- 避免重複送出按鈕操作，以降低 `409 Conflict`。
- 操作成功後，前端應重新查詢或刷新 MLOT 資料，不要只依本地數值推算狀態。
- `MLotUNConsume` 後若產品流程要求恢復為 `Wait`，前端需接續呼叫 `MLotStateChange`。
