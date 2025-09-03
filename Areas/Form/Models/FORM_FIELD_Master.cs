using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Models;


[Table("FORM_FIELD_Master")]
public class FORM_FIELD_Master
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; } = Guid.NewGuid();
    
    [Column("FORM_NAME")]
    public string FORM_NAME { get; set; }  
    
    [Column("BASE_TABLE_NAME")]
    public string BASE_TABLE_NAME { get; set; }  
    
    [Column("VIEW_TABLE_NAME")]
    public string VIEW_TABLE_NAME { get; set; }
    
    [Column("BASE_TABLE_ID")]
    public Guid? BASE_TABLE_ID { get; set; }
    
    [Column("VIEW_TABLE_ID")]
    public Guid? VIEW_TABLE_ID { get; set; }
    
    [Column("STATUS")]
    public int STATUS { get; set; }  
    
    [Column("SCHEMA_TYPE")]
    public TableSchemaQueryType SCHEMA_TYPE { get; set; }
}
