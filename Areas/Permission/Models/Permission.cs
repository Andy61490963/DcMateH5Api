using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClassLibrary;

namespace DcMateH5Api.Areas.Permission.Models
{
    /// <summary>
    /// 功能權限。
    /// </summary>
    [Table("SYS_PERMISSION")]
    public class PermissionModel
    {
        /// <summary>
        /// 權限唯一識別碼。
        /// </summary>
        [Key]
        [Column("ID")]
        public Guid Id { get; set; }

        /// <summary>
        /// 權限代碼，例如：FormDesigner.Edit。
        /// </summary>
        [Column("CODE")]
        public ActionType Code { get; set; }

        /// <summary>
        /// 權限是否啟用。
        /// </summary>
        [Column("IS_ACTIVE")]
        public bool IsActive { get; set; } = true;
    }
}
