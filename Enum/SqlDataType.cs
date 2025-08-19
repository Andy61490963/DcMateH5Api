using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

/// <summary>
/// 系統支援的 SQL 資料型別列舉，用於欄位與控制元件邏輯比對。
/// </summary>
public enum SqlDataType 
{
    Unknown = 0,

    /// <summary>整數型別</summary>
    Int,

    /// <summary>小數型別</summary>
    Decimal,

    /// <summary>布林</summary>
    Bit,

    /// <summary>文字（nvarchar）</summary>
    NVarChar,

    /// <summary>文字（varchar）</summary>
    VarChar,

    /// <summary>日期時間</summary>
    DateTime,

    /// <summary>文字（text）</summary>
    Text,
}
