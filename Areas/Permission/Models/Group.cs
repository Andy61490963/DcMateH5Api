using System;

namespace DynamicForm.Areas.Permission.Models
{
    /// <summary>
    /// 群組資訊。
    /// </summary>
    public class Group
    {
        /// <summary>
        /// 群組唯一識別碼。
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 群組名稱。
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
