using System;

namespace DynamicForm.Areas.Permission.Models
{
    /// <summary>
    /// 側邊選單。
    /// </summary>
    public class Menu
    {
        /// <summary>唯一識別碼。</summary>
        public Guid Id { get; set; }

        /// <summary>父節點。</summary>
        public Guid? ParentId { get; set; }

        /// <summary>功能 ID。</summary>
        public Guid? SysFunctionId { get; set; }

        /// <summary>側邊欄名稱。</summary>
        public string? Name { get; set; }

        /// <summary>排序。</summary>
        public int Sort { get; set; }

        /// <summary>是否共用。</summary>
        public bool IsShare { get; set; }

        /// <summary>是否刪除。</summary>
        public bool IsDelete { get; set; }
    }
}

