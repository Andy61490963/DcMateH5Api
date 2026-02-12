namespace DcMateH5Api.Services.Cache // 命名空間：放置快取相關服務
{
    /// <summary>快取設定參數</summary> // 提供快取服務使用的設定
    public class CacheOptions 
    {
        public bool Enabled { get; init; } = true;
        public int DefaultTtlMinutes { get; set; } = 30; // 預設快取存活時間(分鐘)，預設為30
    } 
} 
