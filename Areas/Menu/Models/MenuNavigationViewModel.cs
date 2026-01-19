using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DCMATEH5API.Areas.Menu.Models
{
    // 1. 最外層容器：對應 JSON 的 "pages"
    public class MenuResponse
    {
        // 將原本的 public Dictionary<string, PageFolderViewModel> Pages 修改如下
        public Dictionary<string, PageFolderViewModel> MenuList { get; set; } = new Dictionary<string, PageFolderViewModel>();

        // PageList 通常存放扁平化的頁面清單，用於快速搜尋
        public List<TileViewModel> PageList { get; set; } = new List<TileViewModel>();
    }

    // 2. 舊版單頁結構：對應 "index.html": { ... }
    public class PageFolderViewModel
    {
        public string? Parameter { get; set; } 

        public string PageKind { get; set; } = "MENU";
        public string? BackUrl { get; set; }
        public string Sid { get; set; } = string.Empty;
        public string Property { get; set; } = "MENU";
        public string TypeGroup { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Lv { get; set; } // 帶出 LV

        public string ImgIcon { get; set; } = string.Empty; // 新增 ImgIcon
        public string? Desc { get; set; } //  DESC
        [JsonPropertyName("tiles")]
        public List<TileViewModel> Tiles { get; set; } = new();
    }

    // 3. 磁磚結構：對應 tiles 陣列內容
    public class TileViewModel
    {
        public string? Parameter { get; set; }

        public string Sid { get; set; } = string.Empty;
        public string Property { get; set; } = "MENU";
        public string TypeGroup { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Seq { get; set; }
        public int Pos { get; set; }
        public int Lv { get; set; } // 帶出 LV

        public string ImgIcon { get; set; } = string.Empty; // 新增 ImgIcon
        public string? Desc { get; set; }

    }

    // 4. 原始資料零件 (接 SQL 用)
    public class MenuNavigationViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Parameter { get; set; } // 新增 Parameter 欄位
        public int Lv { get; set; } // 新增 LV 欄位
        public List<MenuNavigationViewModel> Children { get; set; } = new();
        public string? Desc { get; set; }
        public string ImgIcon { get; set; } = string.Empty; // 新增 ImgIcon 接收 SQL 欄位
        [JsonIgnore]
        public string ParentId { get; set; } = "00000000-0000-0000-0000-000000000000";
        [JsonIgnore]
        public int SortOrder { get; set; }
        [JsonIgnore]
        public string SourceType { get; set; } = string.Empty; // 用來區分 MENU 或 PAGE
    }

    // API 回傳標準包裝
    public class MenuResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string User { get; set; } = "leo";  // Hard Code
        public string LV { get; set; } = "2";     // Hard Code
        public MenuResponse? Data { get; set; }
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