using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Security.Models;

/// <summary>
/// 註冊碼設定資料
/// </summary>
[Table("CFG_REGISTER")]
public class CfgRegisterDto
{
    /// <summary>
    /// 主鍵 SID
    /// </summary>
    [Key]
    [Column("CFG_REGISTER_SID")]
    public decimal CFG_REGISTER_SID { get; set; }

    /// <summary>
    /// 註冊碼
    /// </summary>
    [Column("REGCODE")]
    public string REGCODE { get; set; } = string.Empty;

    /// <summary>
    /// 建立人
    /// </summary>
    [Column("CREATE_USER")]
    public string? CREATE_USER { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    [Column("CREATE_TIME")]
    public DateTime? CREATE_TIME { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    [Column("EDIT_USER")]
    public string? EDIT_USER { get; set; }

    /// <summary>
    /// 修改時間
    /// </summary>
    [Column("EDIT_TIME")]
    public DateTime? EDIT_TIME { get; set; }

    /// <summary>
    /// 驗證碼
    /// </summary>
    [Column("CHECK_CODE")]
    public string? CHECK_CODE { get; set; }

    /// <summary>
    /// 客戶名稱
    /// </summary>
    [Column("CUSTOMER_NAME")]
    public string? CUSTOMER_NAME { get; set; }

    /// <summary>
    /// Token 序號
    /// </summary>
    [Column("TOKEN_SEQ")]
    public int? TOKEN_SEQ { get; set; }
}