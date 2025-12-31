using System.Collections.Generic; // <--- 必須加上這一行，否則 List 會報錯
using System.Text.Json.Serialization; // 讓 [JsonIgnore] 縮短一點

namespace DCMATEH5API.Areas.Menu.Models
{
    public class MenuNavigationViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Translate { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool ExactMatch { get; set; } = true;
        
        public List<MenuNavigationViewModel> Children { get; set; } = new();

        [JsonIgnore]
        public string ParentId { get; set; } = "0"; // 預設為 "0"
        
        [JsonIgnore]
        public int SortOrder { get; set; }
    }

}