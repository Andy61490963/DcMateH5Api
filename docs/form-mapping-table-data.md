# FormMultipleMappingController 取得關聯表資料 API 流程說明

此文件說明 `GET /Form/FormMultipleMapping/{formMasterId}/mapping-table` 與 `PUT /Form/FormMultipleMapping/{formMasterId}/mapping-table` API 的運作流程、主要模組與注意事項。

## 業務需求摘要
- 以 `FormMasterId`（對應 `FORM_FIELD_MASTER.MAPPING_TABLE_ID`）為參數。
- 先查出對應的 `MAPPING_TABLE_NAME`。
- 動態查詢該表的所有資料列，並回傳「欄位名稱 / 值」的結構化 JSON。
- 支援動態更新指定欄位，且必須使用主鍵欄位與 `FormMasterId` 作為更新條件。
- 全流程避免 `dynamic`，改以模型封裝。

## 執行流程
### 取得資料 (GET)
1. **控制器入口**：`FormMultipleMappingController.GetMappingTableData`
   - 驗證 `FormMasterId` 不可為空。
   - 委派 `IFormMultipleMappingService.GetMappingTableData` 取得資料；`InvalidOperationException` 會回傳 400。
2. **服務層查詢**：`FormMultipleMappingService.GetMappingTableData`
   - 以 `FormMasterId` 查詢 `FORM_FIELD_MASTER` 的 `MAPPING_TABLE_NAME`（限定 `IS_DELETE = 0`）。
   - 檢查表名格式（僅允許英數與底線）並透過 `ISchemaService.GetFormFieldMaster` 確認欄位存在，避免無效的資料表存取。
   - 使用 **Dapper** 查詢 `SELECT * FROM [MAPPING_TABLE_NAME]`，逐筆轉為 `MappingTableRowViewModel`，以 `Dictionary<string, object?>` 保存欄位與值。
   - 將結果封裝為 `MappingTableDataViewModel` 回傳，確保資料結構化且可序列化。

### 更新資料 (PUT)
1. **控制器入口**：`FormMultipleMappingController.UpdateMappingTableData`
   - 驗證 `FormMasterId` 與 Request Body 不可為空。
   - 委派 `IFormMultipleMappingService.UpdateMappingTableData` 進行更新；`InvalidOperationException` 會回傳 400。
2. **服務層更新**：`FormMultipleMappingService.UpdateMappingTableData`
   - 以 `FormMasterId` 查詢 `FORM_FIELD_MASTER` 的 `MAPPING_TABLE_NAME`（限定 `IS_DELETE = 0`）。
   - 取得關聯表主鍵欄位與欄位型別，驗證 `Columns / Values` 數量一致且欄位合法。
   - 排除主鍵與 Identity 欄位更新，所有值以 `DynamicParameters` 參數化。
   - 使用 `FormMasterId` 對應的主鍵值作為 `WHERE` 條件，更新單筆資料。
   - 無可更新欄位時不執行 SQL，回傳 `Affected = 0`。

## 資料結構
- `MappingTableDataViewModel`
  - `FormMasterId`：呼叫者的參數（`MAPPING_TABLE_ID`）。
  - `MappingTableName`：實際查到的關聯表名稱。
  - `Rows`：`MappingTableRowViewModel` 集合。
- `MappingTableRowViewModel`
  - `Columns`：大小寫不敏感的字典，鍵為欄位名稱、值為欄位值。
- `MappingTableUpdateRequest`
  - `Columns`：欲更新的欄位名稱清單。
  - `Values`：對應欄位值清單（順序需對齊）。

## 防呆與錯誤處理
- `FormMasterId` 為空：立即回傳 400。
- 查無 `MAPPING_TABLE_NAME` 或欄位資訊：拋出 `InvalidOperationException`，控制器回傳 400。
- 表名格式非法（避免 SQL 注入）：拋出 `InvalidOperationException`。
- `Columns / Values` 數量不一致：拋出 `InvalidOperationException`。
- 欄位不存在、欄位重複、更新主鍵或 Identity 欄位：拋出 `InvalidOperationException`。
- 支援 `CancellationToken`，長時間查詢可被中斷。

## 可重用性與維護性
- 表名檢核使用專屬 `ValidateTableName`，未來如需支援 schema 或更多命名規則，可集中調整。
- 資料列輸出統一使用 `MappingTableRowViewModel`，方便其他 API 重用同一模型或擴充欄位（例如新增型別資訊）。
- 透過 `ISchemaService` 取得欄位清單，避免硬編碼欄位，後續調整資料表結構時不需改動查詢程式碼。
- 更新流程集中在 `UpdateMappingTableData`，欄位驗證與參數化規則一致，便於跨 API 共用與擴充。
