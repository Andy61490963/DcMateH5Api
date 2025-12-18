namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// 多對多表單主檔設定的傳輸模型，負責描述主表、目標表與關聯表的配置。
/// </summary>
public class MultipleMappingFormHeaderViewModel
{
    /// <summary>
    /// FORM_FIELD_Master 主檔唯一識別碼。
    /// </summary>
    public Guid ID { get; set; }

    /// <summary>
    /// 表單顯示名稱，用於前端呈現。
    /// </summary>
    public string FORM_NAME { get; set; } = string.Empty;

    /// <summary>
    /// 主表 FORM_FIELD_Master ID。
    /// </summary>
    public Guid BASE_TABLE_ID { get; set; }

    /// <summary>
    /// 目標多選表（右側）的 FORM_FIELD_Master ID。
    /// </summary>
    public Guid DETAIL_TABLE_ID { get; set; }

    /// <summary>
    /// 關聯表（Mapping） FORM_FIELD_Master ID。
    /// </summary>
    public Guid MAPPING_TABLE_ID { get; set; }

    /// <summary>
    /// 多對多檢視表 FORM_FIELD_Master ID，若無則留空。
    /// </summary>
    public Guid? VIEW_TABLE_ID { get; set; }

    /// <summary>
    /// 關聯表指向主表（Base）的外鍵欄位名稱。
    /// </summary>
    public string MAPPING_BASE_FK_COLUMN { get; set; } = string.Empty;

    /// <summary>
    /// 關聯表指向目標表（Detail）的外鍵欄位名稱。
    /// </summary>
    public string MAPPING_DETAIL_FK_COLUMN { get; set; } = string.Empty;
}
