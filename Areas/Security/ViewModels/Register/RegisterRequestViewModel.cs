namespace DynamicForm.Areas.Security.ViewModels
{
    /// <summary>
    /// 登入請求內容。
    /// </summary>
    public record RegisterRequestViewModel
    {
        public required string Account { get; init; }
        public required string Password { get; init; }
    }
}
