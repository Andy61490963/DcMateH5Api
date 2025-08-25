using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Test.Models
{
    /// <summary>
    /// 使用者帳號資訊
    /// </summary>
    [Table("SYS_USER")]
    public class UserAccount
    {
        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        [Key]                
        [Column("ID")]
        public Guid Id { get; set; }

        /// <summary>
        /// 登入帳號
        /// </summary>
        [Column("AC")]
        public string Account { get; set; } = string.Empty;
        
        /// <summary>
        /// 使用者名稱
        /// </summary>
        [Column("NAME")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 密碼雜湊值（對應資料表 SWD）
        /// </summary>
        [Column("SWD")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Base64 編碼的鹽值（對應資料表 SWD_SALT）
        /// </summary>
        [Column("SWD_SALT")]
        public string PasswordSalt { get; set; } = string.Empty;
        
        /// <summary>
        /// 系統角色(備註)
        /// </summary>
        [Column("ROLE")]
        public string Role { get; set; } = string.Empty;
    }
}
