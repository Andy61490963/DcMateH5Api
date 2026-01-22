namespace DcMateH5Api.Areas.Form.ViewModels;

public class DbColumnInfo
{
    public string COLUMN_NAME { get; set; } = "";
    public string DATA_TYPE { get; set; } = "";
    public int ORDINAL_POSITION { get; set; }
    
    // "YES"/"NO"
    public string IS_NULLABLE { get; set; } = "YES";

    public bool SourceIsNullable =>
        string.Equals(IS_NULLABLE, "YES", StringComparison.OrdinalIgnoreCase);
}
