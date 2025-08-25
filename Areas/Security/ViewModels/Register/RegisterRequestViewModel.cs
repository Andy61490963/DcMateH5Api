using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DcMateH5Api.Areas.Security.ViewModels
{
    /// <summary>
    /// 登入請求內容。
    /// </summary>
    [Table("SYS_USER")]
    public record RegisterRequestViewModel
    {
        [Column("AC")]
        public required string Account { get; init; }
        
        [Column("SWD")]
        public required string Password { get; init; }
    }
}
