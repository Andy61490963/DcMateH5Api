using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Security.Models
{
    /// <summary>
    /// 使用者帳號資訊
    /// </summary>
    [Table("ADM_USER")]
    public class UserAccount
    {
        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        [Key]
        [Column("USER_SID")]
        public Guid Id { get; set; }

        /// <summary>
        /// 登入帳號
        /// </summary>
        [Column("ACCOUNT_NO")]
        public string Account { get; set; } = string.Empty;
        
        /// <summary>
        /// 使用者名稱
        /// </summary>
        [Column("USER_NAME")]
        public string Name { get; init; } = string.Empty;
        
        /// <summary>
        /// 使用者別名
        /// </summary>
        [Column("NICKNAME")]
        public string NickName { get; init; } = string.Empty;
        
        /// <summary>
        /// 不知道啥欄位
        /// </summary>
        [Column("EMP_NO")]
        public string EmpNo { get; init; } = string.Empty;
        
        /// <summary>
        /// 部門
        /// </summary>
        [Column("DEPT_SID")]
        public decimal DeptSid { get; init; }
        
        /// <summary>
        /// 職位稱呼
        /// </summary>
        [Column("TITLE_SID")]
        public decimal TitleSid { get; init; } 
        
        /// <summary>
        /// 不知道啥欄位
        /// </summary>
        [Column("SECURITY_ID")]
        public decimal SecurityId { get; init; }
        
        /// <summary>
        /// 公司名稱
        /// </summary>
        [Column("COMPANY")]
        public string Company { get; init; } = string.Empty;
        
        /// <summary>
        /// 密碼雜湊值（對應資料表 SWD）
        /// </summary>
        [Column("PWD")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Base64 編碼的鹽值（對應資料表 SWD_SALT）
        /// </summary>
        [Column("SECOND_PWD")]
        public string PasswordSalt { get; set; } = string.Empty;
        
        [Column("LV")]
        public string? Lv { get; set; }

    }
}
