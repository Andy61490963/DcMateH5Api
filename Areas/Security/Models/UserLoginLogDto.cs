using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Security.Models
{
    /// <summary>
    /// 使用者登入紀錄
    /// </summary>
    [Table("ADM_USER_LOGIN_LOG")]
    public class UserLoginLogDto
    {
        /// <summary>
        /// 此次登入的唯一識別碼
        /// </summary>
        [Key]
        [Column("ADM_USER_LOGIN_LOG_SID")]
        public Guid ADM_USER_LOGIN_LOG_SID { get; set; }

        /// <summary>
        /// 使用者 ID
        /// </summary>
        [Column("ADM_USER_SID")]
        public Guid ADM_USER_SID { get; set; }

        /// <summary>
        /// 帳號
        /// </summary>
        [Column("ACCOUNT_NO")]
        public string ACCOUNT_NO { get; set; } = string.Empty;

        /// <summary>
        /// 登入時間
        /// </summary>
        [Column("LOGIN_TIME")]
        public DateTime LOGIN_TIME { get; set; }

        /// <summary>
        /// 最後活動時間
        /// </summary>
        [Column("LAST_ACTIVE_TIME")]
        public DateTime LAST_ACTIVE_TIME { get; set; }

        /// <summary>
        /// 登出時間
        /// </summary>
        [Column("LOGOUT_TIME")]
        public DateTime? LOGOUT_TIME { get; set; }

        /// <summary>
        /// 來源 IP 位址
        /// </summary>
        [Column("IP_ADDRESS")]
        public string? IP_ADDRESS { get; set; }
    }
}
