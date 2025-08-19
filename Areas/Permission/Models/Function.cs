using System;

namespace DynamicForm.Areas.Permission.Models
{
    /// <summary>
    /// 系統功能。
    /// </summary>
    public class Function
    {
        /// <summary>唯一識別碼。</summary>
        public Guid Id { get; set; }

        /// <summary>功能名稱。</summary>
        public string? Name { get; set; }

        /// <summary>區域名稱。</summary>
        public string? Area { get; set; }

        /// <summary>控制器名稱。</summary>
        public string? Controller { get; set; }
        
        /// <summary>預設端點</summary>
        public string? DEFAULT_ENDPOINT { get; set; }

        /// <summary>是否刪除。</summary>
        public bool IsDelete { get; set; }
    }
}

