using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Permission.Models
{
    /// <summary>
    /// 系統功能。
    /// </summary>
    [Table("SYS_FUNCTION")]
    public class Function
    {
        /// <summary>
        /// 唯一識別碼
        /// </summary>
        [Key]
        [Column("ID")]
        public Guid Id { get; set; }

        /// <summary>功能名稱。</summary>
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        /// <summary>區域名稱。</summary>
        [Column("AREA")]
        public string Area { get; set; } = string.Empty;

        /// <summary>控制器名稱。</summary>
        [Column("CONTROLLER")]
        public string Controller { get; set; } = string.Empty;
        
        /// <summary>預設端點</summary>
        [Column("DEFAULT_ENDPOINT")]
        public string? DefaultEndpoint { get; set; }
    }
}

