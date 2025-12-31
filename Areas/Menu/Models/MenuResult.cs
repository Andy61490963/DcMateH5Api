namespace DCMATEH5API.Areas.Menu.Models
{
    public class MenuResult
    {
        public bool Success { get; set; }
        // 給予預設空字串，解決 Message 的警告
        public string Message { get; set; } = string.Empty;
        // 使用 object 讓 Data 可以容納 List<MenuNavigationViewModel>
        // 將 Data 宣告為 object? (可為 Null)，因為 API 出錯時 Data 確實可能是空的
        // 或者給予一個預設值，解決 Data 的警告
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}