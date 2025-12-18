using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.Services.FormLogic;

public class FormFieldMasterService : IFormFieldMasterService
{
    private readonly SqlConnection _con;

    public FormFieldMasterService(SqlConnection connection)
    {
        _con = connection;
    }

    public FormFieldMasterDto? GetFormFieldMaster(TableSchemaQueryType type)
    {
        return _con.QueryFirstOrDefault<FormFieldMasterDto>(
            "/**/SELECT * FROM FORM_FIELD_Master WHERE SCHEMA_TYPE = @TYPE",
            new { TYPE = type.ToInt() });
    }

    public FormFieldMasterDto GetFormFieldMasterFromId(Guid? id, SqlTransaction? tx = null)
    {
        return _con.QueryFirst<FormFieldMasterDto>(
            "/**/SELECT * FROM FORM_FIELD_Master WHERE ID = @id",
            new { id }, transaction: tx);
    }

    public List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)> GetFormMetaAggregates( FormFunctionType funcType, TableSchemaQueryType type )
    {
        var masters = _con.Query<FormFieldMasterDto>(
            "/**/SELECT * FROM FORM_FIELD_Master WHERE SCHEMA_TYPE = @TYPE AND FUNCTION_TYPE = @funcType",
            new { TYPE = type.ToInt(), funcType = funcType.ToInt() })
            .ToList();

        var result = new List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)>();

        foreach (var master in masters)
        {
            var configs = _con.Query<FormFieldConfigDto>(
                "/**/SELECT ID, COLUMN_NAME, CONTROL_TYPE, CAN_QUERY FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id",
                new { id = master.BASE_TABLE_ID })
                .ToList();

            result.Add((master, configs));
        }

        return result;
    }
}
