using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

/// <summary>
/// SQL 條件運算子類型
/// </summary>
public enum ConditionType
{
    /// <summary>
    /// 無
    /// </summary>
    [Display(Name = "無")]
    None = 0,
    
    /// <summary>
    /// 等於
    /// </summary>
    [Display(Name = "等於")]
    Equal = 1,
    
    /// <summary>
    /// 模糊比對
    /// </summary>
    [Display(Name = "包含")]
    Like = 2,
    
    /// <summary>
    /// 區間
    /// </summary>
    [Display(Name = "區間")]
    Between = 3,
    
    /// <summary>
    /// 大於
    /// </summary>
    [Display(Name = "大於")]
    GreaterThan = 4,
    
    /// <summary>
    /// 大於或等於
    /// </summary>
    [Display(Name = "大於等於")]
    GreaterThanOrEqual = 5,
    
    /// <summary>
    /// 小於
    /// </summary>
    [Display(Name = "小於")]
    LessThan = 6,
    
    /// <summary>
    /// 小於或等於
    /// </summary>
    [Display(Name = "小於等於")]
    LessThanOrEqual = 7,
    
    /// <summary>
    /// 包含在清單中
    /// </summary>
    [Display(Name = "包含於")]
    In = 8,
    
    /// <summary>
    /// 不等於
    /// </summary>
    [Display(Name = "不等於")]
    NotEqual = 9,
    
    /// <summary>
    /// 不包含在清單中
    /// </summary>
    [Display(Name = "不包含於")]
    NotIn = 10
}