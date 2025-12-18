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
| POST | `/Form/FormMultipleMapping/search` | 取得可用的主檔資料列，回傳的 `Pk` 可直接當作 `baseId` 使用。 | 與一般表單查詢相同，功能類型固定為 `MultipleMappingMaintenance`。 |
| GET | `/Form/FormMultipleMapping/masters` | 取得多對多設定檔清單。 | 僅回傳 FUNCTION_TYPE = MultipleMappingMaintenance；含表名與 FK 欄位。 |
| GET | `/Form/FormMultipleMapping/{formMasterId}/items?baseId=...` | 依設定檔與 Base 主鍵取得「已關聯 / 未關聯」左右清單。 | Base 主鍵會先驗證存在性，再依 Mapping/Base FK 取得左側，NOT EXISTS 取得右側。 |
| POST | `/Form/FormMultipleMapping/{formMasterId}/items` | 右 → 左：批次新增關聯。 | 逐筆檢查 Base/Detail 存在，並以 `IF NOT EXISTS` 避免重複插入。 |
| POST | `/Form/FormMultipleMapping/{formMasterId}/items/remove` | 左 → 右：批次移除關聯。 | 逐筆檢查 Base/Detail 存在後執行刪除。 |

## 核心邏輯與驗證

1. **欄位與資料表檢查**
   - 儲存設定時會驗證 Mapping/Base/Detail 表實體存在，且 Mapping/Base/Detail 皆包含設定的 FK 欄位名稱。 【F:Areas/Form/Services/FormDesignerService.cs†L1093-L1127】
   - 執行操作時再次驗證欄位名稱格式（僅允許英數與底線）與欄位存在性，避免錯誤設定或 SQL Injection。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L170-L203】

2. **主鍵解析與型別轉換**
   - 透過 `ResolvePk` 取得 PK 名稱與 SQL 型別，並用 `ConvertPkType` 將傳入字串轉成正確型別，避免型別錯誤。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L72-L105】【F:DcMateClassLibrary/Helper/FormHelper/ConvertToColumnTypeHelper.cs†L1-L70】

3. **左右清單產出**
   - 左側（已關聯）：依 Mapping.BaseFK = BaseId 取得 Detail FK 清單，再以 `IN` 撈出 Detail 全欄位資料並包裝為 `MultipleMappingItemViewModel`。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L83-L105】【F:Areas/Form/Services/FormMultipleMappingService.cs†L207-L217】
   - 右側（未關聯）：以 `NOT EXISTS` 方式排除已關聯資料，確保與左側互斥。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L219-L232】

4. **取得 Base 清單**
   - `/search` 端點呼叫 `FormMultipleMappingService.GetForms`，以 `FormFunctionType.MultipleMappingMaintenance` 為固定功能類型委派到共用的 `FormService.GetFormList`，確保回傳欄位與資料行為與其他表單一致。 【F:Areas/Form/Controllers/FormMultipleMappingController.cs†L24-L46】【F:Areas/Form/Services/FormMultipleMappingService.cs†L64-L69】

5. **批次新增/移除**
   - 透過 `ITransactionService` 保證同一批次在單一交易內完成。
   - 新增時使用 `IF NOT EXISTS ... INSERT` 避免重複關聯；移除時逐筆 `DELETE`，均會先確認 Base/Detail 資料存在。 【F:Areas/Form/Services/FormMultipleMappingService.cs†L109-L140】【F:Areas/Form/Services/FormMultipleMappingService.cs†L144-L168】

## 程式流程補充

1. 前端先呼叫 `/search`，帶入指定的 `FormMasterId` 與查詢條件取得 Base 清單，並從結果中的 `Pk` 取出目標主鍵值。
2. 前端將取得的 `Pk` 填入 `baseId`，呼叫 `/items` 相關端點取得左右清單或進行新增/移除。
3. 左右清單的計算與新增/移除流程如上節說明，均會在讀寫前做欄位與資料存在性檢查，並使用交易確保一致性。

## 預防常見問題

- **欄位命名錯誤**：欄位名稱會驗證格式並檢查資料表是否存在，避免「欄位不存在」的 SQL 例外。
- **主鍵型別不符**：所有主鍵皆以 `ResolvePk` + `ConvertPkType` 處理，避免 `nvarchar` 與 `uniqueidentifier` 等型別轉換錯誤。
- **重複關聯**：新增時採用 `IF NOT EXISTS`，可避免重複插入造成唯一鍵衝突。

## 相關檔案

- 設計端：`Areas/Form/Controllers/FormDesignerMultipleMappingController.cs`、`Areas/Form/Services/FormDesignerService.cs`
- 執行端：`Areas/Form/Controllers/FormMultipleMappingController.cs`、`Areas/Form/Services/FormMultipleMappingService.cs`
- 模型：`Areas/Form/ViewModels/MultipleMappingOperationViewModels.cs`
