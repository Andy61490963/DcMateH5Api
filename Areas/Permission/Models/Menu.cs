using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Permission.Models
{
    /// <summary>
    /// 側邊選單。
    /// </summary>
    [Table("SYS_MENU")]
    public class Menu
    {
        /// <summary>唯一識別碼。</summary>
        [Key]
        [Column("ID")]
        public Guid Id { get; set; }

        /// <summary>父節點。</summary>
        [Column("PARENT_ID")]
        public Guid? ParentId { get; set; }

        /// <summary>功能 ID。</summary>
        [Column("SYS_FUNCTION_ID")]
        public Guid? SysFunctionId { get; set; }

        /// <summary>側邊欄名稱。</summary>
        [Column("NAME")]
        public string? Name { get; set; }

        /// <summary>排序。</summary>
        [Column("SORT")]
        public int Sort { get; set; }

        /// <summary>是否共用。</summary>
        [Column("IS_SHARE")]
        public bool IsShare { get; set; }
    }
}

