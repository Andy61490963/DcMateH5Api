# LOT API 串接手冊

本文提供前端串接 LOT 模組使用。所有端點皆回傳 `Result<bool>`，成功時 `isSuccess = true` 且 `data = true`。

Base path:

```http
/api/Wip/WipLotSetting
```

## 通用欄位

| 欄位 | 說明 | 限制 |
|---|---|---|
| `LOT` | LOT 批號。 | 必填；必須存在於 `WIP_LOT`，但 `CreateLot` 時必須尚未存在。 |
| `DATA_LINK_SID` | 前端/表單資料的關聯 SID，用來串回來源資料。 | 必填；必須大於 `0`；同一次測試請避免重複使用。 |
| `REPORT_TIME` | 前端回報交易時間。 | 可不填；不填時後端使用 DB 現在時間。格式建議 `yyyy-MM-ddTHH:mm:ss`。 |
| `ACCOUNT_NO` | 操作人員帳號。 | 必填；必須存在於使用者主檔。 |
| `INPUT_FORM_NAME` | 呼叫來源表單名稱。 | 選填；建議填 `Swagger` 或前端頁面名稱，方便查履歷。 |
| `COMMENT` | 備註。 | 選填。 |
| `REASON_SID` | 原因主檔 SID。 | Hold、Release、Bonus、Scrap、StateChange 類必填；必須是啟用中的 `ADM_REASON.ADM_REASON_SID`。 |

目前可用 reason:

| REASON_SID | REASON_NO | REASON_NAME |
|---:|---|---|
| 1 | 1 | 刮痕 |
| 2 | 2 | 來貨不良 |
| 3 | 3 | 混裝 |
| 4 | 4 | 短裝 |
| 5 | 5 | 操損 |
| 6 | 6 | 黏模 |
| 99 | 99 | 其他 |

## 建議測試順序

1. `CreateLot`
2. `LotCheckIn`
3. `LotCheckInCancel` 或 `LotCheckOut`
4. `LotHold`
5. 查 DB 取得 `WIP_LOT_HOLD_HIST.WIP_LOT_HOLD_HIST_SID`，帶入 `LotHoldRelease.LOT_HOLD_SID`
6. `LotBonus` / `LotScrap`
7. `LotStateChange` 或 `LotTerminated` / `LotFinished` 相關 API

注意：同一筆 LOT 狀態會被 API 改變，若要重跑 Swagger 測試，建議換新的 `LOT` 與 `DATA_LINK_SID`。

## CreateLot

Endpoint:

```http
POST /api/Wip/WipLotSetting/CreateLot
```

功能：建立 LOT 主檔，寫入 `CREATE_LOT` 與 `OPER_START` 兩筆 `WIP_LOT_HIST`，並增加工單 `WIP_WO.RELEASE_QTY`。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `LOT` | 新 LOT 批號。 | 必填；不可已存在。 |
| `ALIAS_LOT1` | LOT 別名 1。 | 選填。 |
| `ALIAS_LOT2` | LOT 別名 2。 | 選填。 |
| `WO` | 工單號。 | 必填；必須存在於 `WIP_WO`。 |
| `ROUTE_SID` | 使用的途程 SID。 | 必填；必須存在於 `WIP_ROUTE.WIP_ROUTE_SID`，且要有起始站點。 |
| `LOT_QTY` | LOT 數量。 | 必填；必須大於 `0`。 |

測試 payload:

```json
{
  "DATA_LINK_SID": 900000000900,
  "LOT": "SWAGGER-LOT-001",
  "ALIAS_LOT1": "SWAGGER-LOT-001-A1",
  "ALIAS_LOT2": "SWAGGER-LOT-001-A2",
  "WO": "HC260407004",
  "ROUTE_SID": 158772156102000,
  "LOT_QTY": 2,
  "REPORT_TIME": "2026-04-22T10:00:00",
  "ACCOUNT_NO": "TestAc1",
  "INPUT_FORM_NAME": "Swagger",
  "COMMENT": "swagger create lot test"
}
```

## LotCheckIn

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotCheckIn
```

功能：LOT 進站，狀態由 `Wait` 變為 `Run`，寫入目前人員/設備與 `CHECK_IN` 履歷。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `EQP_NO` | 設備編號。 | 選填；填了就必須存在於設備主檔。 |
| `SHIFT_SID` | 班別 SID。 | 選填；若前端知道班別可帶入。 |
| `LOT_SUB_STATUS_CODE` | LOT 子狀態。 | 選填；例如 `NORMAL`。 |

限制：

- LOT 必須存在。
- LOT 目前狀態需可進站，正常情境為 `Wait`。

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "DATA_LINK_SID": 900000000901,
  "REPORT_TIME": "2026-04-22T10:05:00",
  "ACCOUNT_NO": "TestAc1",
  "EQP_NO": "MC1",
  "SHIFT_SID": 1,
  "LOT_SUB_STATUS_CODE": "NORMAL",
  "COMMENT": "swagger lot check in test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotCheckInCancel

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotCheckInCancel
```

功能：取消進站，清除目前人員/設備，關閉未出站的人員履歷，狀態回到 `Wait`。

限制：

- LOT 必須已進站。
- LOT 目前狀態通常需為 `Run`。

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "DATA_LINK_SID": 900000000902,
  "REPORT_TIME": "2026-04-22T10:10:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot check in cancel test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotCheckOut

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotCheckOut
```

功能：LOT 出站，寫入 `CHECK_OUT`、`OPER_END`，若還有下一站會再寫 `OPER_START` 並回到 `Wait`；若已最後一站則變為 `Finished`。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `EQP_NO` | 出站設備編號。 | 選填；填了就必須存在。 |
| `SHIFT_SID` | 出站班別 SID。 | 選填。 |
| `GROUP_IN_USER` | 是否群組進站人員一起出站。 | `true` 代表同站未出站人員一起處理；`false` 只處理本次帳號。 |

限制：

- LOT 必須已進站。
- LOT 通常需為 `Run`。

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "DATA_LINK_SID": 900000000903,
  "REPORT_TIME": "2026-04-22T10:15:00",
  "ACCOUNT_NO": "TestAc1",
  "EQP_NO": "MC1",
  "SHIFT_SID": 1,
  "GROUP_IN_USER": false,
  "COMMENT": "swagger lot check out test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotHold

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotHold
```

功能：將 LOT Hold，寫入 `LOT_HOLD`、`WIP_LOT_HOLD_HIST`。

限制：

- LOT 目前只允許 `Wait` 或 `Run` 進入 Hold。
- LOT 已經是 `Hold` 時不可再次 Hold。
- `REASON_SID` 必須是啟用中的原因。

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "REASON_SID": 1,
  "DATA_LINK_SID": 900000000904,
  "REPORT_TIME": "2026-04-22T10:20:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot hold test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotHoldRelease

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotHoldRelease
```

功能：解除指定 Hold 紀錄，寫入 `LOT_HOLD_RELEASE`，LOT 狀態恢復為 Hold 前狀態。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `LOT_HOLD_SID` | Hold 紀錄 SID。 | 必填；請查 `WIP_LOT_HOLD_HIST.WIP_LOT_HOLD_HIST_SID` 且 `RELEASE_FLAG = 'N'`。 |

限制：

- LOT 目前必須是 `Hold`。
- 系統採單層 Hold 模型，同一 LOT 正常只能有一筆未解除 Hold。
- 若同一 LOT 有多筆未解除 Hold，後端會視為資料異常並拒絕。

取得 `LOT_HOLD_SID` 範例 SQL:

```sql
SELECT WIP_LOT_HOLD_HIST_SID
FROM WIP_LOT_HOLD_HIST
WHERE LOT = 'SWAGGER-LOT-001'
  AND RELEASE_FLAG = 'N';
```

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "LOT_HOLD_SID": 123456789,
  "REASON_SID": 2,
  "DATA_LINK_SID": 900000000905,
  "REPORT_TIME": "2026-04-22T10:25:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot hold release test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotReassignOperation

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotReassignOperation
```

功能：重派 LOT 目前所在站點，寫入 `LOT_RESSIGN_OPER`、`OPER_END`、`OPER_START`，並更新 LOT 目前站點。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `NEW_OPER_SEQ` | 新站點序號。 | 必填；必須存在於該 LOT route 的 `WIP_ROUTE_OPERATION.SEQ`。 |

限制：

- LOT 必須存在。
- 目標站點必須屬於 LOT 目前 route。

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "DATA_LINK_SID": 900000001001,
  "NEW_OPER_SEQ": 2,
  "REPORT_TIME": "2026-04-22T15:00:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger reassign operation test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotRecordDC

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotRecordDC
```

功能：記錄 LOT DC 量測資料，寫入 `WIP_LOT_DC_ITEM_HIST`，並更新目前量測值 `WIP_LOT_DC_ITEM_CURRENT`。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `ACTION_CODE` | 履歷 action code。 | 可填 `LOT_RECORD_DC`；空白時後端使用預設值。 |
| `DC_TYPE` | DC 類型。 | 例如 `IPQC`。 |
| `ITEMS` | 量測項目清單。 | 至少 1 筆。 |
| `ITEMS[].DC_ITEM_CODE` | 量測項目代碼。 | 若未帶 `DC_ITEM_SID`，需用此欄查 `QMM_DC_ITEM.QMM_ITEM_NO`。 |
| `ITEMS[].DC_ITEM_VALUE` | 量測值。 | 字串格式，前端依量測項目型別給值。 |
| `ITEMS[].DC_ITEM_COMMENT` | 單項量測備註。 | 選填。 |

測試 payload:

```json
{
  "ACTION_CODE": "LOT_RECORD_DC",
  "DC_TYPE": "IPQC",
  "LOT": "SWAGGER-LOT-001",
  "DATA_LINK_SID": 900000001002,
  "ACCOUNT_NO": "TestAc1",
  "EQP_NO": "MC1",
  "SHIFT_SID": 1,
  "REPORT_TIME": "2026-04-22T15:10:00",
  "COMMENT": "swagger lot dc test",
  "INPUT_FORM_NAME": "Swagger",
  "ITEMS": [
    {
      "DC_ITEM_CODE": "Default",
      "DC_ITEM_VALUE": "12.34",
      "DC_ITEM_COMMENT": "measured in swagger"
    }
  ]
}
```

## LotBonus

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotBonus
```

功能：追加 LOT 數量，寫入 `LOT_BONUS` 與 `WIP_LOT_REASON_HIST`。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `BONUS_QTY` | 追加數量。 | 必填；必須大於 `0`。 |

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "BONUS_QTY": 1,
  "REASON_SID": 1,
  "DATA_LINK_SID": 900000001003,
  "REPORT_TIME": "2026-04-22T15:20:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot bonus test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotScrap

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotScrap
```

功能：減少 LOT 數量並增加 `NG_QTY`，寫入 `LOT_NG` 與 `WIP_LOT_REASON_HIST`。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `SCRAP_QTY` | 報廢/減少數量。 | 必填；必須大於 `0`，且不可大於目前 `LOT_QTY`。 |

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "SCRAP_QTY": 1,
  "REASON_SID": 2,
  "DATA_LINK_SID": 900000001004,
  "REPORT_TIME": "2026-04-22T15:25:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot scrap test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotStateChange

Endpoint:

```http
POST /api/Wip/WipLotSetting/LotStateChange
```

功能：通用 LOT 狀態切換，寫入 `LOT_STATE_CHANGE` 與原因履歷。

| 欄位 | 說明 | 限制 |
|---|---|---|
| `NEW_STATE_CODE` | 目標 LOT 狀態。 | 必填；必須存在於 `WIP_LOT_STATUS.LOT_STATUS_CODE`。 |

測試 payload:

```json
{
  "LOT": "SWAGGER-LOT-001",
  "NEW_STATE_CODE": "Terminated",
  "REASON_SID": 1,
  "DATA_LINK_SID": 900000001005,
  "REPORT_TIME": "2026-04-22T15:30:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot state change test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## LotTerminated / LotUnTerminated / LotFinished / LotUnFinished

Endpoints:

```http
POST /api/Wip/WipLotSetting/LotTerminated
POST /api/Wip/WipLotSetting/LotUnTerminated
POST /api/Wip/WipLotSetting/LotFinished
POST /api/Wip/WipLotSetting/LotUnFinished
```

功能：狀態切換的固定版 API，前端不需要傳 `NEW_STATE_CODE`。

| API | 狀態限制 | 目標狀態 | ACTION_CODE |
|---|---|---|---|
| `LotTerminated` | 目前必須是 `Wait` | `Terminated` | `LOT_TERMINATED` |
| `LotUnTerminated` | 目前必須是 `Terminated` | `Wait` | `LOT_UNTERMINATED` |
| `LotFinished` | 目前必須是 `Wait` | `Finished` | `LOT_FINISHED` |
| `LotUnFinished` | 目前必須是 `Finished` | `Wait` | `LOT_UNFINISHED` |

測試 payload 範例，四支 API body 相同：

```json
{
  "LOT": "SWAGGER-LOT-001",
  "REASON_SID": 1,
  "DATA_LINK_SID": 900000001006,
  "REPORT_TIME": "2026-04-22T15:35:00",
  "ACCOUNT_NO": "TestAc1",
  "COMMENT": "swagger lot fixed state action test",
  "INPUT_FORM_NAME": "Swagger"
}
```

## 回傳格式

成功:

```json
{
  "isSuccess": true,
  "code": "Success",
  "message": null,
  "data": true
}
```

失敗:

```json
{
  "isSuccess": false,
  "code": "BadRequest",
  "message": "LOT is required.",
  "data": false
}
```

HTTP status 會依錯誤類型回 `400`、`409` 或 `500`。
