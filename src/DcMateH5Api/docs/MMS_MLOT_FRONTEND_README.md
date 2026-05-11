# MMS MLOT 前端串接說明

本文說明 MMS 模組中 MLOT 相關 API 的用途、LOT 與 MLOT 的關係、欄位限制、建議呼叫流程與 Swagger 測試 payload。

## 1. LOT 與 MLOT 的關係

### LOT 是什麼

`LOT` 是製程中的在製批號，屬於 WIP 模組資料，主要記錄一批產品在工單、途程、站點、進站、出站、Hold、Finished 等製程狀態。

例子：

一張工單 `HC260407004` 要生產某個成品，前端呼叫 `CreateLot` 建立 `SWAGGER-LOT-001`。這個 `LOT` 會在製程站點中進站、出站，最後呼叫 `LotFinished` 代表這批製程完成。

### MLOT 是什麼

`MLOT` 是物料/庫存批號，屬於 MMS 模組資料，主要記錄完成品、半成品或可被後續工序消耗的庫存批次與數量。

例子：

`SWAGGER-LOT-001` 完工後產出 10 PCS 半成品，前端接著呼叫 `CreateMLot` 建立 `SWAGGER-MLOT-001`，代表這 10 PCS 變成可被後續 LOT 使用或消耗的庫存。

### 兩者差異

| 類型 | LOT | MLOT |
| --- | --- | --- |
| 模組 | WIP | MMS |
| 代表意義 | 製程中的批號 | 庫存/物料批號 |
| 主要狀態 | Wait、Run、Hold、Finished、Terminated | Wait、Finished |
| 主要數量 | LOT_QTY | MLOT_QTY |
| 常見流程 | CreateLot -> CheckIn -> CheckOut -> Finished | CreateMLot -> Consume -> UNConsume -> StateChange |
| 是否一定互相關聯 | CreateMLot 本身不直接傳 LOT | Consume/UNConsume 會用 LOT + MLOT 建立或移除使用關係 |

### 建議流程

一般前端流程會是：

1. `CreateLot` 建立 WIP LOT。
2. LOT 完成製程後呼叫 `LotFinished`。
3. `LotFinished` 成功後呼叫 `CreateMLot`，把完成的 LOT 產出轉成 MLOT 庫存。
4. 後續其他 LOT 要使用這批物料時，呼叫 `MLotConsume`，帶入使用者正在作業的 `LOT` 與要消耗的 `MLOT`。
5. 若誤消耗，呼叫 `MLotUNConsume` 加回庫存。
6. 若只需要人工改 MLOT 狀態，呼叫 `MLotStateChange`。

目前沒有做 `LotFinished + CreateMLot` 的後端合併 API，前端需要分兩次呼叫

## 2. API 共通規則

Base route：

```http
/api/MMS/MmsLot
```

所有 API 成功與失敗都回 `Result<bool>` 結構。

成功範例：

```json
{
  "IsSuccess": true,
  "Code": "Success",
  "Message": null,
  "Data": true
}
```

失敗範例：

```json
{
  "IsSuccess": false,
  "Code": "BadRequest",
  "Message": "MLOT not found: SWAGGER-MLOT-001",
  "Data": false
}
```

常見 HTTP status：

| Status | 代表意義 |
| --- | --- |
| 200 | 成功 |
| 400 | payload 缺欄位、資料不存在、數量不足、狀態主檔不存在 |
| 409 | MLOT 重複建立或 optimistic concurrency 更新失敗 |
| 500 | 未預期錯誤 |

## 3. 主檔前置條件

`MMS_MLOT_STATUS` 必須至少有以下狀態：

| MLOT_STATUS_CODE | 用途 |
| --- | --- |
| Wait | MLOT 可用/待使用 |
| Finished | MLOT 已用完 |

若資料庫尚未補主檔，請先執行：

```text
src/DcMateH5Api/docs/MMS_MLOT_STATUS_SEED.sql
```

## 4. CreateMLot

Endpoint：

```http
POST /api/MMS/MmsLot/CreateMLot
```

用途：

建立一筆 MLOT 庫存。通常在 `LotFinished` 成功後呼叫，用來把完工 LOT 的產出建立成可被後續消耗的庫存批號。

後端行為：

| 行為 | 說明 |
| --- | --- |
| 驗證 MLOT 不可重複 | 若已存在回 409 |
| 驗證使用者存在 | `ACCOUNT_NO` 必須存在於 `ADM_OPI_USER` |
| 驗證狀態主檔 | `Wait` 必須存在於 `MMS_MLOT_STATUS` |
| 建立主檔 | 新增 `MMS_MLOT` |
| 建立履歷 | 新增 `MMS_MLOT_HIST`，`ACTION_CODE = MLOT_CREATE` |
| 初始狀態 | 固定 `MLOT_STATUS_CODE = Wait` |

欄位說明：

| 欄位 | 必填 | 說明 | 限制 |
| --- | --- | --- | --- |
| DATA_LINK_SID | 是 | 前端交易識別 SID，用於追蹤來源交易 | 必須大於 0 |
| MLOT | 是 | 要建立的 MLOT 批號 | 不可空白、不可重複 |
| PARENT_MLOT | 否 | 父層 MLOT | 未傳時後端使用自己的 `MLOT` |
| ALIAS_MLOT1 | 否 | MLOT 別名 1 | 可空 |
| ALIAS_MLOT2 | 否 | MLOT 別名 2 | 可空 |
| MLOT_TYPE | 否 | MLOT 類型 | 未傳時後端預設 `N` |
| PART_NO | 是 | 料號 | 不可空白 |
| MLOT_QTY | 是 | 初始庫存數量 | 必須大於 0 |
| MLOT_WO | 否 | 來源工單 | 建議填入 LOT 所屬工單 |
| EXPIRY_DATE | 否 | 有效期限 | ISO datetime |
| DATE_CODE | 否 | Date code | 可空 |
| REPORT_TIME | 否 | 前端回報時間 | 未傳時後端使用 DB 現在時間 |
| ACCOUNT_NO | 是 | 操作人員帳號 | 必須存在 |
| INPUT_FORM_NAME | 否 | 來源畫面名稱 | 建議填 `Swagger` 或前端頁面代碼 |
| COMMENT | 否 | 備註 | 可空 |

測試 payload：

```json
{
  "DATA_LINK_SID": 900000004001,
  "MLOT": "SWAGGER-MLOT-001",
  "PARENT_MLOT": null,
  "ALIAS_MLOT1": "SWAGGER-MLOT-001-A1",
  "ALIAS_MLOT2": "SWAGGER-MLOT-001-A2",
  "MLOT_TYPE": "N",
  "PART_NO": "HC-PART-001",
  "MLOT_QTY": 10,
  "MLOT_WO": "HC260407004",
  "EXPIRY_DATE": "2026-12-31T23:59:59",
  "DATE_CODE": "20260511",
  "REPORT_TIME": "2026-05-11T10:00:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "swagger create mlot test"
}
```

最小 payload：

```json
{
  "DATA_LINK_SID": 900000004001,
  "MLOT": "SWAGGER-MLOT-001",
  "PART_NO": "HC-PART-001",
  "MLOT_QTY": 10,
  "ACCOUNT_NO": "TestAc1"
}
```

## 5. MLotConsume

Endpoint：

```http
POST /api/MMS/MmsLot/MLotConsume
```

用途：

讓某一筆 WIP LOT 消耗指定 MLOT 庫存。例如某個製程 LOT 要使用一批半成品或物料，就呼叫此 API 扣庫存。

後端行為：

| 行為 | 說明 |
| --- | --- |
| 驗證 MLOT 存在 | 不存在回 400 |
| 驗證 LOT 存在 | 不存在回 400 |
| 驗證數量 | `CONSUME_QTY` 必須大於 0，且不可大於目前 `MLOT_QTY` |
| 扣減庫存 | `MMS_MLOT.MLOT_QTY -= CONSUME_QTY` |
| 狀態判斷 | 扣完剩 0 改 `Finished`，否則維持/改成 `Wait` |
| 建立履歷 | `MMS_MLOT_HIST.ACTION_CODE = MLOT_CONSUME`，`TRANSATION_QTY` 為負數 |
| 建立目前使用關係 | 若 `WIP_LOT_KP_CUR_USED` 無該 `LOT + MLOT`，新增一筆 |

欄位說明：

| 欄位 | 必填 | 說明 | 限制 |
| --- | --- | --- | --- |
| DATA_LINK_SID | 是 | 前端交易識別 SID | 必須大於 0 |
| LOT | 是 | 消耗 MLOT 的 WIP LOT | 必須存在於 `WIP_LOT` |
| MLOT | 是 | 被消耗的 MLOT | 必須存在於 `MMS_MLOT` |
| CONSUME_QTY | 是 | 消耗數量 | 必須大於 0，且不可超過目前 MLOT 庫存 |
| REPORT_TIME | 否 | 前端回報時間 | 未傳時後端使用 DB 現在時間 |
| ACCOUNT_NO | 是 | 操作人員帳號 | 必須存在 |
| INPUT_FORM_NAME | 否 | 來源畫面名稱 | 可空 |
| COMMENT | 否 | 備註 | 可空 |

測試 payload：

```json
{
  "DATA_LINK_SID": 900000004002,
  "LOT": "SWAGGER-LOT-001",
  "MLOT": "SWAGGER-MLOT-001",
  "CONSUME_QTY": 4,
  "REPORT_TIME": "2026-05-11T10:05:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "swagger mlot consume test"
}
```

扣到 0 的 payload：

假設 `SWAGGER-MLOT-001` 原本 10，前一次已扣 4，剩 6。再送以下 payload 會讓 MLOT 狀態變成 `Finished`。

```json
{
  "DATA_LINK_SID": 900000004003,
  "LOT": "SWAGGER-LOT-001",
  "MLOT": "SWAGGER-MLOT-001",
  "CONSUME_QTY": 6,
  "REPORT_TIME": "2026-05-11T10:10:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "swagger consume mlot to zero"
}
```

## 6. MLotUNConsume

Endpoint：

```http
POST /api/MMS/MmsLot/MLotUNConsume
```

用途：

取消前面的 MLOT 消耗，將數量加回 MLOT 庫存，並移除 LOT 與 MLOT 的目前使用關係。

後端行為：

| 行為 | 說明 |
| --- | --- |
| 驗證 MLOT 存在 | 不存在回 400 |
| 驗證 LOT 存在 | 不存在回 400 |
| 驗證數量 | `UNCONSUME_QTY` 必須大於 0 |
| 加回庫存 | `MMS_MLOT.MLOT_QTY += UNCONSUME_QTY` |
| 建立履歷 | `MMS_MLOT_HIST.ACTION_CODE = MLOT_UNCONSUME`，`TRANSATION_QTY` 為正數 |
| 移除目前使用關係 | 刪除 `WIP_LOT_KP_CUR_USED` 中該 `LOT + MLOT` |
| 狀態規則 | 沿用舊邏輯，不會自動把 `Finished` 改回 `Wait` |

重要限制：

如果 MLOT 已經因扣到 0 變成 `Finished`，呼叫 `MLotUNConsume` 加回數量後，狀態仍會維持 `Finished`。若前端需要把狀態改回 `Wait`，請再呼叫 `MLotStateChange`。

測試 payload：

```json
{
  "DATA_LINK_SID": 900000004004,
  "LOT": "SWAGGER-LOT-001",
  "MLOT": "SWAGGER-MLOT-001",
  "UNCONSUME_QTY": 3,
  "REPORT_TIME": "2026-05-11T10:15:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "swagger mlot unconsume test"
}
```

## 7. MLotStateChange

Endpoint：

```http
POST /api/MMS/MmsLot/MLotStateChange
```

用途：

人工變更 MLOT 狀態，只改狀態，不改庫存數量。常見用途是將 `Finished` 改回 `Wait`，或配合管理流程調整狀態。

後端行為：

| 行為 | 說明 |
| --- | --- |
| 驗證 MLOT 存在 | 不存在回 400 |
| 驗證新狀態存在 | `NEW_MLOT_STATE_CODE` 必須存在於 `MMS_MLOT_STATUS` |
| Reason 可選 | `REASON_CODE` 可不傳；若傳入且查得到 `ADM_REASON.REASON_NO`，才寫入 reason 欄位 |
| 建立履歷 | `MMS_MLOT_HIST.ACTION_CODE = MLOT_STATE_CHANGE` |
| 不改數量 | `MLOT_QTY` 不會異動 |

欄位說明：

| 欄位 | 必填 | 說明 | 限制 |
| --- | --- | --- | --- |
| DATA_LINK_SID | 是 | 前端交易識別 SID | 必須大於 0 |
| MLOT | 是 | 要改狀態的 MLOT | 必須存在 |
| NEW_MLOT_STATE_CODE | 是 | 新 MLOT 狀態 | 必須存在於 `MMS_MLOT_STATUS` |
| REASON_CODE | 否 | 原因代碼 | 可空；查得到才寫入 |
| REPORT_TIME | 否 | 前端回報時間 | 未傳時後端使用 DB 現在時間 |
| ACCOUNT_NO | 是 | 操作人員帳號 | 必須存在 |
| INPUT_FORM_NAME | 否 | 來源畫面名稱 | 可空 |
| COMMENT | 否 | 備註 | 可空 |

測試 payload：

```json
{
  "DATA_LINK_SID": 900000004005,
  "MLOT": "SWAGGER-MLOT-001",
  "NEW_MLOT_STATE_CODE": "Wait",
  "REASON_CODE": "1",
  "REPORT_TIME": "2026-05-11T10:20:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "swagger mlot state change test"
}
```

不帶 reason 的 payload：

```json
{
  "DATA_LINK_SID": 900000004006,
  "MLOT": "SWAGGER-MLOT-001",
  "NEW_MLOT_STATE_CODE": "Finished",
  "REPORT_TIME": "2026-05-11T10:25:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "manual finish mlot"
}
```

## 8. 常見錯誤

| 錯誤訊息 | 原因 | 處理方式 |
| --- | --- | --- |
| `MLOT already exists` | MLOT 批號重複 | 換一個新的 MLOT |
| `MLOT status not found: Wait` | `MMS_MLOT_STATUS` 缺主檔 | 執行 seed SQL |
| `MLOT not found` | Consume/UNConsume/StateChange 指定的 MLOT 不存在 | 先 CreateMLot 或修正 MLOT |
| `LOT not found` | Consume/UNConsume 指定的 LOT 不存在 | 先 CreateLot 或修正 LOT |
| `MLOT_QTY is insufficient` | 消耗數量大於目前庫存 | 降低 `CONSUME_QTY` 或確認庫存 |
| `User not found` | `ACCOUNT_NO` 不存在 | 改用有效帳號 |