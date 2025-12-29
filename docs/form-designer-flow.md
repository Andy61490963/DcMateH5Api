# 表單設計功能流程說明

## 功能類型 (FUNCTION_TYPE)
- **MasterMaintenance (主檔維護)**：僅維護 BASE/VIEW，對應 `FormDesignerController` 與 `FormController`。
- **MasterDetailMaintenance (一對多維護)**：維護 BASE/DETAIL/VIEW，對應 `FormDesignerMasterDetailController` 與 `FormMasterDetailController`。
- **MultipleMappingMaintenance (多對多維護)**：維護 BASE/DETAIL/MAPPING/VIEW，對應 `FormDesignerMultipleMappingController`。

## 主要 API 與流程
1. **列表/名稱維護/刪除**
   - 依 `FUNCTION_TYPE` 分流，與既有單表/主明細一致，透過 `GetFormMasters`、`UpdateFormName`、`Delete` 等 API。 【F:Areas/Form/Controllers/FormDesignerMultipleMappingController.cs†L10-L105】

2. **欄位設定**
   - `EnsureFieldsSaved` 會在查詢欄位時自動建立草稿設定，支援 `TableSchemaQueryType.OnlyMapping`，並套用與主表相同的查詢/編輯策略。 【F:Areas/Form/Services/FormDesignerService.cs†L437-L519】

3. **設計入口**
   - `GetFormDesignerIndexViewModel` 依 `FUNCTION_TYPE` 組合 Base/Detail/Mapping/View 欄位：  
     - MasterMaintenance：Base + View  
     - MasterDetailMaintenance：Base + Detail + View  
     - MultipleMappingMaintenance：Base + Detail + Mapping (+View 可選)  
   - 未配對的功能模組會直接回報錯誤。 【F:Areas/Form/Services/FormDesignerService.cs†L103-L205】

4. **表頭儲存**
   - 多對多：`SaveMultipleMappingFormHeader` 會確認三張表存在、檢查關聯欄位（依設定尾碼比對），並以 `UpsertMultipleMappingFormMaster` 寫回 `FORM_FIELD_MASTER`。View 可選，提供顯示用途。 【F:Areas/Form/Services/FormDesignerService.cs†L1072-L1149】【F:Areas/Form/Services/FormDesignerService.cs†L1270-L1360】
   - 若有設定 `MAPPING_BASE_COLUMN_NAME` / `MAPPING_DETAIL_COLUMN_NAME`，會額外驗證該顯示欄位是否存在於主表與目標表，避免前端顯示欄位查不到資料。 【F:Areas/Form/Services/FormDesignerService.cs†L1379-L1476】
   - 單表/一對多沿用原有 `SaveFormHeader`、`SaveMasterDetailFormHeader`，並帶入 `FUNCTION_TYPE`。 【F:Areas/Form/Services/FormDesignerService.cs†L946-L1053】

5. **先前查詢下拉值匯入**
   - `ImportPreviousQueryDropdownValues` 僅接受 `SELECT`，且結果欄位需使用 `AS NAME`。成功後會將 NAME 清單序列化寫入 `FORM_FIELD_DROPDOWN.DROPDOWNSQL`，並將 `IS_QUERY_DROPDOWN` 設為 `1`。 【F:Areas/Form/Controllers/FormDesignerController.cs†L410-L430】【F:Areas/Form/Services/FormDesignerService.cs†L1135-L1218】
   - `FormService.BuildFieldViewModel` 會解析 `DROPDOWNSQL` 轉成 `PREVIOUS_QUERY_LIST` 回傳給前端。 【F:Areas/Form/Services/FormService.cs†L391-L447】

## 資料表/列舉調整
- `FormFunctionType`：新增 `MultipleMappingMaintenance`，並以 Display 名稱標示三種模式。 【F:DcMateClassLibrary/Enum/FormFunctionType.cs†L1-L17】
- `TableSchemaQueryType`：新增 `OnlyMapping (4)`，保留既有數值，避免舊資料斷層。 【F:DcMateClassLibrary/Enum/TableSchemaQueryType.cs†L1-L19】
- 新增 Swagger 群組 `FormWithMultipleMapping`，便於前後端對應。 【F:DcMateClassLibrary/Helper/SwaggerGroups.cs†L9-L31】

## 使用建議
- 建議在建立多對多設計時，先以 `EnsureFieldsSaved` 產出 Base/Detail/Mapping 欄位設定，再呼叫 `SaveMultipleMappingFormHeader` 完成表頭綁定。
- 關聯表需包含與主表、目標表共用且以設定尾碼結尾的欄位（例如 `_ID`），否則服務會拒絕儲存。 【F:Areas/Form/Services/FormDesignerService.cs†L1116-L1126】
