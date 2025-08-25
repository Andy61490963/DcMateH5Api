using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Permission.Models
{
    /// <summary>
    /// 群組資訊。
    /// </summary>
    [Table("SYS_GROUP")]
    public class Group
    {
        /// <summary>
        /// 群組唯一識別碼
        /// </summary>
        [Key]
        [Column("ID")]
        public Guid Id { get; set; }

        /// <summary>
        /// 群組名稱
        /// </summary>
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 群組是否啟用
        /// </summary>
        [Column("IS_ACTIVE")]
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// 群組是否啟用
        /// </summary>
        [Column("DESCRIPTION")]
        public string? Description { get; set; }
    }
}
