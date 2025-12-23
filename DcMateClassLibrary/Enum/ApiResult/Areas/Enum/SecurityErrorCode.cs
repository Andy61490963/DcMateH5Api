using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;


public enum EnumErrorCode
{
    [Display(Name = "找不到指定的列舉名稱，請後端確認是否在白名單")]
    EnumNotWhitelisted
}