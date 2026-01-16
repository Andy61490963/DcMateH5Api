using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Security.Models
{
    /// <summary>
    /// 使用者帳號資訊
    /// </summary>
    [Table("SEC_USER")]
    public class UserAccount
    {
        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        [Key]
        [Column("USER_SID")]
        public decimal Id { get; set; }

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
        /// 密碼雜湊值（對應資料表 SWD）
        /// </summary>
        [Column("PWD")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Base64 編碼的鹽值（對應資料表 SWD_SALT）
        /// </summary>
        [Column("SECOND_PWD")]
        public string PasswordSalt { get; set; } = string.Empty;
        
        public int? LV { get; set; }

    }
}
