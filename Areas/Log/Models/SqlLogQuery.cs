namespace DcMateH5Api.Areas.Log.Models
{
    public class SqlLogQuery
    {
        public Guid? UserId { get; set; }
        public Guid? RequestId { get; set; }
        public bool? IsSuccess { get; set; }

        public DateTime? ExecutedFrom { get; set; }
        public DateTime? ExecutedTo { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}