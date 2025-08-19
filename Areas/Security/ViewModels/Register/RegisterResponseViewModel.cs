namespace DynamicForm.Areas.Security.ViewModels
{
    /// <summary>
    /// 登入請求內容。
    /// </summary>
    public record RegisterResponseViewModel
    {
        public Guid UserId { get; set; }
        public string Account { get; set; }
        public string Role { get; set; }
    }
}
