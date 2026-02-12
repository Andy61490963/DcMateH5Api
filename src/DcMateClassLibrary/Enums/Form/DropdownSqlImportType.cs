namespace DcMateClassLibrary.Enums.Form;

public enum DropdownSqlImportType
{
    /// <summary>
    /// 用於「編輯欄位」的下拉：SQL 必須回傳 ID + NAME
    /// </summary>
    EditDropdown,   

    /// <summary>
    /// 用於「先前查詢值」的下拉：SQL 只需回傳 NAME
    /// </summary>
    PreviousQuery
}
