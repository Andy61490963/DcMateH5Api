using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Models;


[Table("FORM_FIELD_MASTER")]
public class FormFieldMasterDto
{
    [Key]
    [Column("ID")]
    public Guid ID { get; set; } = Guid.NewGuid();
    
    
    
    [Column("FORM_NAME")]
    public string FORM_NAME { get; set; }  
    
    [Column("FORM_CODE")]
    public string FORM_CODE { get; set; }  
    
    [Column("FORM_DESCRIPTION")]
    public string FORM_DESCRIPTION { get; set; }  
    
    
    
    [Column("BASE_TABLE_NAME")]
    public string? BASE_TABLE_NAME { get; set; }  
    
    [Column("DETAIL_TABLE_NAME")]
    public string? DETAIL_TABLE_NAME { get; set; }
    
    [Column("VIEW_TABLE_NAME")]
    public string? VIEW_TABLE_NAME { get; set; }
    
    [Column("MAPPING_TABLE_NAME")]
    public string? MAPPING_TABLE_NAME { get; set; }
    
    
    [Column("BASE_TABLE_ID")]
    public Guid? BASE_TABLE_ID { get; set; }
    
    [Column("DETAIL_TABLE_ID")]
    public Guid? DETAIL_TABLE_ID { get; set; }
    
    [Column("VIEW_TABLE_ID")]
    public Guid? VIEW_TABLE_ID { get; set; }

    [Column("MAPPING_TABLE_ID")]
    public Guid? MAPPING_TABLE_ID { get; set; }

    
    
    
    [Column("MAPPING_BASE_FK_COLUMN")]
    public string? MAPPING_BASE_FK_COLUMN { get; set; }

    [Column("MAPPING_DETAIL_FK_COLUMN")]
    public string? MAPPING_DETAIL_FK_COLUMN { get; set; }

    [Column("MAPPING_BASE_COLUMN_NAME")]
    public string? MAPPING_BASE_COLUMN_NAME { get; set; }

    [Column("MAPPING_DETAIL_COLUMN_NAME")]
    public string? MAPPING_DETAIL_COLUMN_NAME { get; set; }

    
    
    [Column("SOURCE_DETAIL_COLUMN")]
    public string? SOURCE_DETAIL_COLUMN { get; set; }
    
    [Column("TARGET_MAPPING_COLUMN")]
    public string? TARGET_MAPPING_COLUMN { get; set; }
    
    
    
    [Column("FUNCTION_TYPE")]
    public FormFunctionType? FUNCTION_TYPE { get; set; }
    
    [Column("STATUS")]
    public int STATUS { get; set; }  
    
    [Column("SCHEMA_TYPE")]
    public TableSchemaQueryType SCHEMA_TYPE { get; set; }
    
    // [Timestamp]
    // [Column("ROW_VERSION")]
    // public byte[] ROW_VERSION { get; set; } = default!;
}
