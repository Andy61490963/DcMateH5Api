using ClassLibrary;
using Dapper;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces.FormLogic;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Services.FormLogic;

public class FormFieldConfigService : IFormFieldConfigService
{
    private readonly SqlConnection _con;
    
    public FormFieldConfigService(SqlConnection connection)
    {
        _con = connection;
    }

    public List<FormFieldConfigDto> GetFormFieldConfig(Guid? id)
    {
        return _con.Query<FormFieldConfigDto>(
            "/**/SELECT ID, COLUMN_NAME, CONTROL_TYPE, QUERY_CONDITION_TYPE, CAN_QUERY FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id",
            new { id }).ToList();
    }
    
    public FieldConfigData LoadFieldConfigData(Guid masterId)
    {
        const string sql = @"SELECT FFC.*, FFM.FORM_NAME
                    FROM FORM_FIELD_CONFIG FFC
                    JOIN FORM_FIELD_Master FFM ON FFM.ID = FFC.FORM_FIELD_Master_ID
                    WHERE FFM.ID = @ID
                    ORDER BY FIELD_ORDER;

                    SELECT R.*
                    FROM FORM_FIELD_VALIDATION_RULE R
                    JOIN FORM_FIELD_CONFIG C ON R.FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_Master_ID = @ID;

                    SELECT D.*
                    FROM FORM_FIELD_DROPDOWN D
                    JOIN FORM_FIELD_CONFIG C ON D.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_Master_ID = @ID;

                    SELECT O.*
                    FROM FORM_FIELD_DROPDOWN_OPTIONS O
                    JOIN FORM_FIELD_DROPDOWN D ON O.FORM_FIELD_DROPDOWN_ID = D.ID
                    JOIN FORM_FIELD_CONFIG C ON D.FORM_FIELD_CONFIG_ID = C.ID
                    WHERE C.FORM_FIELD_Master_ID = @ID;";

        using var multi = _con.QueryMultiple(sql, new { ID = masterId });

        return new FieldConfigData(
            multi.Read<FormFieldConfigDto>().ToList(),
            multi.Read<FormFieldValidationRuleDto>().ToList(),
            multi.Read<FORM_FIELD_DROPDOWN>().ToList(),
            multi.Read<FORM_FIELD_DROPDOWN_OPTIONS>().ToList());
    }
}