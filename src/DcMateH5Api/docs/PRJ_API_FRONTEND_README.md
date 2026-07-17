# PRJ 專案管理 API JSON 欄位說明

Base path：

```http
/api/PRJ
```

日期欄位格式為 ISO 8601，例如 `2026-07-17T10:30:00`。沒有日期時為 `null`。

## API 一覽

| 方法 | Endpoint | Request | Response Data |
|---|---|---|---|
| GET | `/Projects` | Query：`ProjectQuery` | `PagedResult<Project>` |
| GET | `/Projects/{projectCode}` | Route：`projectCode` | `Project` |
| POST | `/Projects` | `CreateProjectRequest` | `Project` |
| PUT | `/Projects/{projectCode}` | `UpdateProjectRequest` | `Project` |
| PATCH | `/Projects/{projectCode}/enabled` | `ChangeEnabledRequest` | `Project` |
| PUT | `/Projects/reorder` | `ReorderProjectsRequest` | `boolean` |
| GET | `/Projects/{projectCode}/Details` | Query：`DetailQuery` | `PagedResult<ProjectDetail>` |
| GET | `/Details/{detailSid}` | Route：`detailSid` | `ProjectDetail` |
| POST | `/Projects/{projectCode}/Details` | `CreateDetailRequest` | `ProjectDetail` |
| PUT | `/Details/{detailSid}` | `UpdateDetailRequest` | `ProjectDetail` |
| PATCH | `/Details/{detailSid}/status` | `ChangeDetailStatusRequest` | `ProjectDetail` |
| PATCH | `/Details/{detailSid}/enabled` | `ChangeEnabledRequest` | `ProjectDetail` |
| PUT | `/Projects/{projectCode}/Details/reorder` | `ReorderDetailsRequest` | `boolean` |
| GET | `/Options` | 無 | `PrjOptions` |
| GET | `/Options/Customers` | Query：`keyword`、`take` | `TextOption[]` |
| GET | `/Options/Users` | Query：`keyword`、`take` | `TextOption[]` |

## 共用回應 JSON

```json
{
  "IsSuccess": true,
  "Data": {},
  "Code": "",
  "Message": "",
  "ErrorData": null
}
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `IsSuccess` | boolean | API 是否成功。 |
| `Data` | object、array、boolean 或 null | API 回傳資料。實際結構依 Endpoint 而定。 |
| `Code` | string | 成功時為空字串；失敗時為錯誤代碼。 |
| `Message` | string | 成功時為空字串；失敗時為錯誤訊息。 |
| `ErrorData` | object 或 null | 額外錯誤資料。 |

錯誤代碼可能為：`BadRequest`、`Unauthorized`、`Forbidden`、`NotFound`、`Conflict`、`UnhandledException`。

## 分頁 JSON

```json
{
  "Items": [],
  "Page": 1,
  "PageSize": 20,
  "TotalCount": 0
}
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `Items` | array | 當頁資料。 |
| `Page` | integer | 目前頁碼，從 1 開始。 |
| `PageSize` | integer | 每頁筆數。 |
| `TotalCount` | integer | 符合條件的總筆數。 |

## 專案查詢欄位

Endpoint：`GET /api/PRJ/Projects`

| Query 欄位 | 型別 | 必填 | 預設值 | 說明 |
|---|---|---|---|---|
| `Page` | integer | 否 | 1 | 頁碼。 |
| `PageSize` | integer | 否 | 20 | 每頁筆數，範圍 1～100。 |
| `Keyword` | string | 否 | null | 比對專案代碼或名稱。 |
| `StatusNo` | number | 否 | null | 專案狀態代碼。 |
| `TypeNo` | number | 否 | null | 專案類型代碼。 |
| `CustomerNo` | string | 否 | null | 客戶代碼。 |
| `Enabled` | boolean | 否 | null | `true` 為啟用、`false` 為停用、null 為全部。 |
| `StartFrom` | datetime | 否 | null | 專案開始日期下限。 |
| `StartTo` | datetime | 否 | null | 專案開始日期上限。 |
| `SortBy` | string | 否 | `Seq` | `Seq`、`ProjectCode`、`ProjectName`、`StartTime`、`ExpectedTime`、`EditTime`。 |
| `SortDescending` | boolean | 否 | false | 是否遞減排序。 |

## 專案 JSON

`GET /Projects` 的 `Data.Items[]`、`GET /Projects/{projectCode}` 及專案異動 API 的 `Data` 使用此結構。

```json
{
  "ProjectSid": 415381781100165,
  "Seq": 1,
  "ProjectCode": "PRJ-2026-001",
  "ProjectName": "MES 導入專案",
  "StatusNo": 2,
  "StatusName": "已接案",
  "TypeNo": 2,
  "TypeName": "客戶",
  "CustomerNo": "ACME",
  "CustomerName": "ACME 公司",
  "StartTime": "2026-07-17T00:00:00",
  "ExpectedTime": "2026-12-31T00:00:00",
  "EndTime": null,
  "IsOrder": "Y",
  "Enabled": true,
  "DetailCount": 10,
  "CompletedDetailCount": 4,
  "OverdueDetailCount": 1,
  "CreateUser": "admin",
  "CreateTime": "2026-07-17T10:00:00",
  "EditUser": "admin",
  "EditTime": "2026-07-17T10:30:15.123"
}
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ProjectSid` | number | 專案 SID。 |
| `Seq` | integer 或 null | 顯示順序。 |
| `ProjectCode` | string | 專案代碼。 |
| `ProjectName` | string 或 null | 專案名稱。 |
| `StatusNo` | number 或 null | 專案狀態代碼。 |
| `StatusName` | string 或 null | 專案狀態名稱。 |
| `TypeNo` | number 或 null | 專案類型代碼。 |
| `TypeName` | string 或 null | 專案類型名稱。 |
| `CustomerNo` | string 或 null | 客戶代碼。 |
| `CustomerName` | string 或 null | 客戶名稱。 |
| `StartTime` | datetime 或 null | 專案開始日期。 |
| `ExpectedTime` | datetime 或 null | 預計完成日期。 |
| `EndTime` | datetime 或 null | 實際完成日期。 |
| `IsOrder` | string 或 null | 既有訂單相關欄位，可能為 `Y`、`N`、訂單編號或 null。 |
| `Enabled` | boolean | 是否啟用。 |
| `DetailCount` | integer | 啟用中的工作總數。 |
| `CompletedDetailCount` | integer | 已完成工作數。 |
| `OverdueDetailCount` | integer | 逾期且未完成工作數。 |
| `CreateUser` | string 或 null | 建立者；列表回應可能不包含。 |
| `CreateTime` | datetime 或 null | 建立時間；列表回應可能不包含。 |
| `EditUser` | string 或 null | 最後修改者；列表回應可能不包含。 |
| `EditTime` | datetime 或 null | 最後修改時間，也是異動 API 的版本欄位。 |

## 建立專案 Request JSON

Endpoint：`POST /api/PRJ/Projects`

```json
{
  "ProjectCode": "PRJ-2026-001",
  "ProjectName": "MES 導入專案",
  "StatusNo": 2,
  "TypeNo": 2,
  "CustomerNo": "ACME",
  "StartTime": "2026-07-17T00:00:00",
  "ExpectedTime": "2026-12-31T00:00:00",
  "EndTime": null,
  "IsOrder": "Y",
  "Seq": 1
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `ProjectCode` | string | 是 | 專案代碼，最大 150 字元且不可重複。 |
| `ProjectName` | string 或 null | 否 | 專案名稱。 |
| `StatusNo` | number 或 null | 否 | 專案狀態代碼。 |
| `TypeNo` | number 或 null | 否 | 專案類型代碼。 |
| `CustomerNo` | string 或 null | 否 | 啟用中的客戶代碼。 |
| `StartTime` | datetime 或 null | 否 | 專案開始日期。 |
| `ExpectedTime` | datetime 或 null | 否 | 預計完成日期。 |
| `EndTime` | datetime 或 null | 否 | 實際完成日期。 |
| `IsOrder` | string 或 null | 否 | 訂單相關內容。 |
| `Seq` | integer 或 null | 否 | 顯示順序。 |

## 修改專案 Request JSON

Endpoint：`PUT /api/PRJ/Projects/{projectCode}`

```json
{
  "ProjectName": "MES 導入專案第二階段",
  "StatusNo": 2,
  "TypeNo": 2,
  "CustomerNo": "ACME",
  "StartTime": "2026-07-17T00:00:00",
  "ExpectedTime": "2027-01-31T00:00:00",
  "EndTime": null,
  "IsOrder": "Y",
  "Seq": 1,
  "EditTime": "2026-07-17T10:30:15.123"
}
```

欄位與建立專案相同，但沒有 `ProjectCode`，並增加下列欄位：

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `EditTime` | datetime | 是 | 最近一次查詢取得的專案修改時間。 |

## 啟用狀態 Request JSON

適用 Endpoint：

- `PATCH /api/PRJ/Projects/{projectCode}/enabled`
- `PATCH /api/PRJ/Details/{detailSid}/enabled`

```json
{
  "Enabled": false,
  "EditTime": "2026-07-17T10:30:15.123"
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `Enabled` | boolean | 是 | 目標啟用狀態。 |
| `EditTime` | datetime | 是 | 最近一次查詢取得的修改時間。 |

## 專案排序 Request JSON

Endpoint：`PUT /api/PRJ/Projects/reorder`

```json
{
  "Items": [
    {
      "ProjectCode": "PRJ-2026-001",
      "Seq": 1,
      "EditTime": "2026-07-17T10:30:15.123"
    }
  ]
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `Items` | array | 是 | 排序項目，至少一筆。 |
| `Items[].ProjectCode` | string | 是 | 專案代碼。 |
| `Items[].Seq` | integer | 是 | 新順序。 |
| `Items[].EditTime` | datetime | 是 | 專案目前修改時間。 |

## 工作明細查詢欄位

Endpoint：`GET /api/PRJ/Projects/{projectCode}/Details`

| Query 欄位 | 型別 | 必填 | 預設值 | 說明 |
|---|---|---|---|---|
| `Page` | integer | 否 | 1 | 頁碼。 |
| `PageSize` | integer | 否 | 20 | 每頁筆數，範圍 1～100。 |
| `Keyword` | string | 否 | null | 比對摘要或備註。 |
| `StatusNo` | number | 否 | null | 工作狀態代碼。 |
| `ProcessTypeNo` | number | 否 | null | 處理類型代碼。 |
| `UserAccount` | string | 否 | null | 比對負責人、支援人員或審核人員帳號。 |
| `Enabled` | boolean | 否 | null | `true` 為啟用、`false` 為停用、null 為全部。 |
| `StartFrom` | datetime | 否 | null | 實際開始日期下限。 |
| `EndTo` | datetime | 否 | null | 實際結束日期上限。 |
| `SortBy` | string | 否 | `Seq` | `Seq`、`Summary`、`StartTime`、`ExpectedTime`、`EndTime`、`EditTime`。 |
| `SortDescending` | boolean | 否 | false | 是否遞減排序。 |

## 工作明細 JSON

```json
{
  "DetailSid": 415381781100001,
  "ProjectCode": "PRJ-2026-001",
  "ProcessTypeNo": 36,
  "ProcessTypeName": "WebApi開發",
  "Summary": "開發專案查詢 API",
  "StatusNo": 1,
  "StatusName": "處理中",
  "IsCompleted": false,
  "IsOverdue": false,
  "Comment": "專案工作備註",
  "PrincipalUser": "developer1",
  "PrincipalUserName": "開發人員",
  "SupportUser": null,
  "SupportUserName": null,
  "ReviewerUser": "reviewer1",
  "ReviewerUserName": "審核人員",
  "StartExpectedTime": "2026-07-17T00:00:00",
  "StartTime": null,
  "ExpectedTime": "2026-07-31T00:00:00",
  "EndTime": null,
  "Seq": 1,
  "Enabled": true,
  "FileName": null,
  "CreateUser": "admin",
  "CreateTime": "2026-07-17T10:00:00",
  "EditUser": "admin",
  "EditTime": "2026-07-17T10:30:15.123"
}
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `DetailSid` | number | 工作明細 SID。 |
| `ProjectCode` | string | 所屬專案代碼。 |
| `ProcessTypeNo` | number 或 null | 處理類型代碼。 |
| `ProcessTypeName` | string 或 null | 處理類型名稱。 |
| `Summary` | string 或 null | 工作摘要。 |
| `StatusNo` | number | 工作狀態代碼。 |
| `StatusName` | string 或 null | 工作狀態名稱。 |
| `IsCompleted` | boolean | 狀態是否屬於完成。 |
| `IsOverdue` | boolean | 是否逾期且未完成。 |
| `Comment` | string 或 null | 備註。 |
| `PrincipalUser` | string 或 null | 負責人帳號。 |
| `PrincipalUserName` | string 或 null | 負責人姓名。 |
| `SupportUser` | string 或 null | 支援人員帳號。 |
| `SupportUserName` | string 或 null | 支援人員姓名。 |
| `ReviewerUser` | string 或 null | 審核人員帳號。 |
| `ReviewerUserName` | string 或 null | 審核人員姓名。 |
| `StartExpectedTime` | datetime 或 null | 預計開始日期。 |
| `StartTime` | datetime 或 null | 實際開始日期。 |
| `ExpectedTime` | datetime 或 null | 預計完成日期。 |
| `EndTime` | datetime 或 null | 實際完成日期。 |
| `Seq` | integer 或 null | 顯示順序。 |
| `Enabled` | boolean | 是否啟用。 |
| `FileName` | string 或 null | 既有檔名欄位；本版沒有附件 API。 |
| `CreateUser` | string 或 null | 建立者。 |
| `CreateTime` | datetime 或 null | 建立時間。 |
| `EditUser` | string 或 null | 最後修改者。 |
| `EditTime` | datetime 或 null | 最後修改時間，也是異動 API 的版本欄位。 |

## 建立工作明細 Request JSON

Endpoint：`POST /api/PRJ/Projects/{projectCode}/Details`

```json
{
  "ProcessTypeNo": 36,
  "Summary": "開發專案查詢 API",
  "StatusNo": 1,
  "Comment": "專案工作備註",
  "PrincipalUser": "developer1",
  "SupportUser": null,
  "ReviewerUser": "reviewer1",
  "StartExpectedTime": "2026-07-17T00:00:00",
  "StartTime": null,
  "ExpectedTime": "2026-07-31T00:00:00",
  "EndTime": null,
  "Seq": 1,
  "FileName": null
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `ProcessTypeNo` | number 或 null | 否 | 處理類型代碼。 |
| `Summary` | string 或 null | 否 | 工作摘要，最大 2000 字元。 |
| `StatusNo` | number | 是 | 工作狀態代碼。 |
| `Comment` | string 或 null | 否 | 備註，最大 2000 字元。 |
| `PrincipalUser` | string 或 null | 否 | 負責人帳號。 |
| `SupportUser` | string 或 null | 否 | 支援人員帳號。 |
| `ReviewerUser` | string 或 null | 否 | 審核人員帳號。 |
| `StartExpectedTime` | datetime 或 null | 否 | 預計開始日期。 |
| `StartTime` | datetime 或 null | 否 | 實際開始日期。 |
| `ExpectedTime` | datetime 或 null | 否 | 預計完成日期。 |
| `EndTime` | datetime 或 null | 否 | 實際完成日期。 |
| `Seq` | integer 或 null | 否 | 顯示順序。 |
| `FileName` | string 或 null | 否 | 既有檔名欄位。 |

## 修改工作明細 Request JSON

Endpoint：`PUT /api/PRJ/Details/{detailSid}`

欄位與建立工作明細相同，並增加：

```json
{
  "ProcessTypeNo": 36,
  "Summary": "完成專案查詢 API",
  "StatusNo": 1,
  "Comment": null,
  "PrincipalUser": "developer1",
  "SupportUser": null,
  "ReviewerUser": "reviewer1",
  "StartExpectedTime": "2026-07-17T00:00:00",
  "StartTime": "2026-07-17T00:00:00",
  "ExpectedTime": "2026-07-31T00:00:00",
  "EndTime": null,
  "Seq": 1,
  "FileName": null,
  "EditTime": "2026-07-17T10:30:15.123"
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `EditTime` | datetime | 是 | 最近一次查詢取得的工作明細修改時間。 |

## 工作狀態 Request JSON

Endpoint：`PATCH /api/PRJ/Details/{detailSid}/status`

```json
{
  "StatusNo": 3,
  "EditTime": "2026-07-17T10:30:15.123"
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `StatusNo` | number | 是 | 新工作狀態代碼。 |
| `EditTime` | datetime | 是 | 最近一次查詢取得的工作明細修改時間。 |

## 工作排序 Request JSON

Endpoint：`PUT /api/PRJ/Projects/{projectCode}/Details/reorder`

```json
{
  "Items": [
    {
      "DetailSid": 415381781100001,
      "Seq": 1,
      "EditTime": "2026-07-17T10:30:15.123"
    }
  ]
}
```

| 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `Items` | array | 是 | 排序項目，至少一筆。 |
| `Items[].DetailSid` | number | 是 | 工作明細 SID。 |
| `Items[].Seq` | integer | 是 | 新順序。 |
| `Items[].EditTime` | datetime | 是 | 工作明細目前修改時間。 |

## 固定選項 Response JSON

Endpoint：`GET /api/PRJ/Options`

```json
{
  "ProjectStatuses": [
    { "Value": 2, "Text": "已接案", "IsCompleted": null }
  ],
  "ProjectTypes": [
    { "Value": 2, "Text": "客戶", "IsCompleted": null }
  ],
  "DetailStatuses": [
    { "Value": 3, "Text": "完成", "IsCompleted": true }
  ],
  "ProcessTypes": [
    { "Value": 36, "Text": "WebApi開發", "IsCompleted": null }
  ]
}
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ProjectStatuses` | array | 專案狀態。 |
| `ProjectTypes` | array | 專案類型。 |
| `DetailStatuses` | array | 工作狀態。 |
| `ProcessTypes` | array | 工作處理類型。 |
| `Value` | number | 選項代碼。 |
| `Text` | string | 選項名稱。 |
| `IsCompleted` | boolean 或 null | 工作狀態是否為完成；其他選項為 null。 |

## 客戶與使用者選項 Response JSON

適用 Endpoint：

- `GET /api/PRJ/Options/Customers?keyword={keyword}&take={take}`
- `GET /api/PRJ/Options/Users?keyword={keyword}&take={take}`

`keyword` 可省略；`take` 預設 20，範圍 1～100。

```json
[
  {
    "Value": "ACME",
    "Text": "ACME 公司"
  }
]
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `Value` | string | 客戶代碼或使用者帳號。 |
| `Text` | string | 客戶名稱或使用者姓名。 |
