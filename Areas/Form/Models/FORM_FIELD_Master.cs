using System.Collections.Generic;
using ClassLibrary;

namespace DynamicForm.Areas.Form.Models;

public class FORM_FIELD_Master
{
    public Guid ID { get; set; } = Guid.NewGuid();
    public string FORM_NAME { get; set; }  
    public string BASE_TABLE_NAME { get; set; }  
    public string VIEW_TABLE_NAME { get; set; }
    public Guid BASE_TABLE_ID { get; set; }
    public Guid VIEW_TABLE_ID { get; set; }
    public int STATUS { get; set; }  
    public TableSchemaQueryType SCHEMA_TYPE { get; set; }  
}
