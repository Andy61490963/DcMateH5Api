using ClassLibrary;
using Dapper;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace DynamicForm.Areas.Form.Services.FormLogic;

public class FormFieldMasterService : IFormFieldMasterService
{
    private readonly SqlConnection _con;

    public FormFieldMasterService(SqlConnection connection)
    {
        _con = connection;
    }

    public FORM_FIELD_Master? GetFormFieldMaster(TableSchemaQueryType type)
    {
        return _con.QueryFirstOrDefault<FORM_FIELD_Master>(
            "/**/SELECT * FROM FORM_FIELD_Master WHERE SCHEMA_TYPE = @TYPE",
            new { TYPE = type.ToInt() });
    }

    public FORM_FIELD_Master GetFormFieldMasterFromId(Guid id, SqlTransaction? tx = null)
    {
        return _con.QueryFirst<FORM_FIELD_Master>(
            "/**/SELECT * FROM FORM_FIELD_Master WHERE ID = @id",
            new { id }, transaction: tx);
    }

    public List<(FORM_FIELD_Master Master, List<FormFieldConfigDto> FieldConfigs)> GetFormMetaAggregates(TableSchemaQueryType type)
    {
        var masters = _con.Query<FORM_FIELD_Master>(
            "/**/SELECT * FROM FORM_FIELD_Master WHERE SCHEMA_TYPE = @TYPE",
            new { TYPE = type.ToInt() })
            .ToList();

        var result = new List<(FORM_FIELD_Master Master, List<FormFieldConfigDto> FieldConfigs)>();

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

