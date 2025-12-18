# 多對多維護 API 說明

本文件描述「多對多維護」在後端的設定儲存流程、查詢邏輯與新增/移除關聯的 API 介面，方便前端與維運人員理解整體運作方式。

## 設定欄位與來源

- **FORM_FIELD_Master 新增欄位**
  - `MAPPING_BASE_FK_COLUMN`：關聯表指向主表（Base）的 FK 欄位名稱。
  - `MAPPING_DETAIL_FK_COLUMN`：關聯表指向明細表（Detail）的 FK 欄位名稱。
  - 欄位由 `FormDesignerMultipleMappingController.SaveMultipleMappingFormHeader` 寫入，並由 `FormMultipleMappingService` 操作關聯表時使用。 【F:Areas/Form/Controllers/FormDesignerMultipleMappingController.cs†L396-L420】【F:Areas/Form/Services/FormDesignerService.cs†L1075-L1153】

- **主鍵解析**
  - Base / Detail 不再把 PK 寫在設定檔，而是於執行期透過 `SchemaService.ResolvePk(tableName)` 取得 PK 名稱與型別，並將前端傳入的主鍵值轉成正確型別。 【F:Areas/Form/Services/FormLogic/SchemaService.cs†L20-L120】

## API 端點

| 方法 | 路徑 | 說明 | 備註 |
| --- | --- | --- | --- |
| GET | `/Form/FormMultipleMapping/masters` | 取得多對多設定檔清單。 | 僅回傳 FUNCTION_TYPE = MultipleMappingMaintenance；含表名與 FK 欄位。 |
| GET | `/Form/FormMultipleMapping/{formMasterId}/items?baseId=...` | 依設定檔與 Base 主鍵取得「已關聯 / 未關聯」左右清單。 | Base 主鍵會先驗證存在性，再依 Mapping/Base FK 取得左側，NOT EXISTS 取得右側。 |
| POST | `/Form/FormMultipleMapping/{formMasterId}/items` | 右 → 左：批次新增關聯。 | 逐筆檢查 Base/Detail 存在，並以 `IF NOT EXISTS` 避免重複插入。 |
| POST | `/Form/FormMultipleMapping/{formMasterId}/items/remove` | 左 → 右：批次移除關聯。 | 逐筆檢查 Base/Detail 存在後執行刪除。 |

## 核心邏輯與驗證

1. **欄位與資料表檢查**
   - 儲存設定時會驗證 Mapping/Base/Detail 表實體存在，且 Mapping/Base/Detail 皆包含設定的 FK 欄位名稱。 【F:Areas/Form/Services/FormDesignerService.cs†L1093-L1127】
   - 執行操作時再次驗證欄位名稱格式（僅允許英數與底線）與欄位存在性，避免錯誤設定或 SQL Injection。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L120-L151】

2. **主鍵解析與型別轉換**
   - 透過 `ResolvePk` 取得 PK 名稱與 SQL 型別，並用 `ConvertPkType` 將傳入字串轉成正確型別，避免型別錯誤。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L40-L94】【F:DcMateClassLibrary/Helper/FormHelper/ConvertToColumnTypeHelper.cs†L1-L70】

3. **左右清單產出**
   - 左側（已關聯）：依 Mapping.BaseFK = BaseId 取得 Detail FK 清單，再以 `IN` 撈出 Detail 全欄位資料並包裝為 `MultipleMappingItemViewModel`。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L61-L87】
   - 右側（未關聯）：以 `NOT EXISTS` 方式排除已關聯資料，確保與左側互斥。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L89-L108】

4. **批次新增/移除**
   - 透過 `ITransactionService` 保證同一批次在單一交易內完成。
   - 新增時使用 `IF NOT EXISTS ... INSERT` 避免重複關聯；移除時逐筆 `DELETE`，均會先確認 Base/Detail 資料存在。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L96-L118】【F:Areas/Form/Services/FormMultipleMappingService.cs†L153-L184】

## 預防常見問題

- **欄位命名錯誤**：欄位名稱會驗證格式並檢查資料表是否存在，避免「欄位不存在」的 SQL 例外。
- **主鍵型別不符**：所有主鍵皆以 `ResolvePk` + `ConvertPkType` 處理，避免 `nvarchar` 與 `uniqueidentifier` 等型別轉換錯誤。
- **重複關聯**：新增時採用 `IF NOT EXISTS`，可避免重複插入造成唯一鍵衝突。

## 相關檔案

- 設計端：`Areas/Form/Controllers/FormDesignerMultipleMappingController.cs`、`Areas/Form/Services/FormDesignerService.cs`
- 執行端：`Areas/Form/Controllers/FormMultipleMappingController.cs`、`Areas/Form/Services/FormMultipleMappingService.cs`
- 模型：`Areas/Form/ViewModels/MultipleMappingOperationViewModels.cs`
