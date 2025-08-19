using ClassLibrary;
using Dapper;
using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces;
using DynamicForm.Helper;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Services;

public class FormListService : IFormListService
{
    private readonly SqlConnection _con;
    
    public FormListService(SqlConnection connection)
    {
        _con = connection;
    }
    
    public List<FORM_FIELD_Master> GetFormMasters()
    {
        var statusList = new[] { TableStatusType.Active, TableStatusType.Disabled };
        return _con.Query<FORM_FIELD_Master>(Sql.FormMasterSelect, new{ STATUS = statusList }).ToList();
    }

    public FORM_FIELD_Master? GetFormMaster(Guid id)
    {
        return _con.QueryFirstOrDefault<FORM_FIELD_Master>(Sql.FormMasterById, new { id });
    }

    public void DeleteFormMaster(Guid id)
    {
        _con.Execute(Sql.DeleteFormMaster, new { id });
    }

    private static class Sql
    {
        public const string FormMasterSelect = @"/**/
SELECT * FROM FORM_FIELD_Master WHERE STATUS IN @STATUS";
        
        public const string FormMasterById   = @"/**/
SELECT * FROM FORM_FIELD_Master WHERE ID = @id";
        
        public const string DeleteFormMaster = @"/**/
DELETE FROM FORM_FIELD_DROPDOWN_OPTIONS WHERE FORM_FIELD_DROPDOWN_ID IN (
    SELECT ID FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID IN (
        SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id
    )
);
DELETE FROM FORM_FIELD_DROPDOWN WHERE FORM_FIELD_CONFIG_ID IN (
    SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id
);
DELETE FROM FORM_FIELD_VALIDATION_RULE WHERE FIELD_CONFIG_ID IN (
    SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id
);
DELETE FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @id;
DELETE FROM FORM_FIELD_Master WHERE ID = @id;
";
    }
}