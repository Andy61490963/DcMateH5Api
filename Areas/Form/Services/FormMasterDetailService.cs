using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace DcMateH5Api.Areas.Form.Services;

/// <summary>
/// 處理主表與明細表 CRUD 的服務。
/// </summary>
public class FormMasterDetailService : IFormMasterDetailService
{
    private readonly SqlConnection _con;
    private readonly IFormService _formService;
    private readonly IFormFieldMasterService _formFieldMasterService;
    private readonly IFormDataService _formDataService;
    private readonly ISchemaService _schemaService;
    private readonly ITransactionService _txService;
    private readonly string _relationColumnSuffix;

    public FormMasterDetailService(
        SqlConnection connection,
        IFormService formService,
        IFormFieldMasterService formFieldMasterService,
        IFormDataService formDataService,
        ISchemaService schemaService,
        ITransactionService txService,
        IConfiguration configuration)
    {
        _con = connection;
        _formService = formService;
        _formFieldMasterService = formFieldMasterService;
        _formDataService = formDataService;
        _schemaService = schemaService;
        _txService = txService;
        _relationColumnSuffix =
            configuration.GetValue<string>("FormSettings:RelationColumnSuffix") ?? "_NO";
    }

    private string GetRelationColumn(string masterTable, string detailTable)
    {
        var masterCols = _schemaService.GetFormFieldMaster(masterTable);
        var detailCols = _schemaService.GetFormFieldMaster(detailTable);
        var common = masterCols.Intersect(detailCols, StringComparer.OrdinalIgnoreCase);
        var col = common.FirstOrDefault(c =>
            c.EndsWith(_relationColumnSuffix, StringComparison.OrdinalIgnoreCase));
        return col ?? throw new InvalidOperationException(
            $"No relation column ending with '{_relationColumnSuffix}' found between {masterTable} and {detailTable}.");
    }

    /// <inheritdoc />
    public List<FormListDataViewModel> GetFormList(FormSearchRequest? request = null)
    {
        return _formService.GetFormList(request);
    }

    /// <inheritdoc />
    public FormMasterDetailSubmissionViewModel GetFormSubmission(Guid formMasterDetailId, string? pk = null)
    {
        // 取得主明細表頭設定，包含主表與明細表的 FormId
        var header = _formFieldMasterService.GetFormFieldMasterFromId(formMasterDetailId);
        var masterVm = _formService.GetFormSubmission(header.MASTER_TABLE_ID, pk);

        var result = new FormMasterDetailSubmissionViewModel
        {
            Master = masterVm,
            Details = new List<FormSubmissionViewModel>()
        };

        // 若為新增，僅回傳空白的明細欄位模板
        if (string.IsNullOrEmpty(pk))
        {
            var emptyDetail = _formService.GetFormSubmission(header.DETAIL_TABLE_ID);
            result.Details.Add(emptyDetail);
            return result;
        }

        var relationColumn = GetRelationColumn(header.MASTER_TABLE_NAME, header.DETAIL_TABLE_NAME);
        var detailPk = _schemaService.GetPrimaryKeyColumn(header.DETAIL_TABLE_NAME)
            ?? throw new InvalidOperationException("Detail table has no primary key.");

        var (pkName, pkType, pkVal) = _schemaService.ResolvePk(header.MASTER_TABLE_NAME, pk);
        var relationObj = _con.ExecuteScalar<object?>(
            $"SELECT [{relationColumn}] FROM [{header.MASTER_TABLE_NAME}] WHERE [{pkName}] = @id",
            new { id = pkVal });
        var relationValue = relationObj?.ToString();
        if (relationValue == null)
            return result;

        var detailColumnTypes = _formDataService.LoadColumnTypes(header.DETAIL_TABLE_NAME);
        var rows = _formDataService.GetRows(
            header.DETAIL_TABLE_NAME,
            new List<FormQueryCondition>
            {
                new FormQueryCondition
                {
                    Column = relationColumn,
                    ConditionType = ConditionType.Equal,
                    Value = relationValue,
                    DataType = detailColumnTypes.GetValueOrDefault(relationColumn, "string")
                }
            });

        foreach (var row in rows)
        {
            var detailPkValue = row[detailPk]?.ToString();
            var detailVm = _formService.GetFormSubmission(header.DETAIL_TABLE_ID, detailPkValue);
            result.Details.Add(detailVm);
        }

        return result;
    }

    /// <inheritdoc />
    public void SubmitForm(FormMasterDetailSubmissionInputModel input)
    {
        _txService.WithTransaction(tx =>
        {
            var header = _formFieldMasterService.GetFormFieldMasterFromId(input.FormId, tx);
            var relationColumn = GetRelationColumn(header.MASTER_TABLE_NAME, header.DETAIL_TABLE_NAME);

            var masterCfgId = _con.ExecuteScalar<Guid?>(
                "SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @Id AND COLUMN_NAME = @Col",
                new { Id = header.MASTER_TABLE_ID, Col = relationColumn }, transaction: tx)
                ?? throw new InvalidOperationException("Master relation column not found.");
            var detailCfgId = _con.ExecuteScalar<Guid?>(
                "SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @Id AND COLUMN_NAME = @Col",
                new { Id = header.DETAIL_TABLE_ID, Col = relationColumn }, transaction: tx)
                ?? throw new InvalidOperationException("Detail relation column not found.");

            var relationValue = input.MasterFields
                .FirstOrDefault(f => f.FieldConfigId == masterCfgId)?.Value;

            if (relationValue == null && !string.IsNullOrEmpty(input.MasterPk))
            {
                var (pkName, pkType, pkVal) = _schemaService.ResolvePk(header.MASTER_TABLE_NAME, input.MasterPk, tx);
                relationValue = _con.ExecuteScalar<object?>(
                    $"SELECT [{relationColumn}] FROM [{header.MASTER_TABLE_NAME}] WHERE [{pkName}] = @id",
                    new { id = pkVal }, tx)?.ToString();
            }

            if (relationValue == null)
                throw new InvalidOperationException("Relation value not provided.");

            if (!input.MasterFields.Any(f => f.FieldConfigId == masterCfgId))
            {
                input.MasterFields.Add(new FormInputField
                {
                    FieldConfigId = masterCfgId,
                    Value = relationValue
                });
            }

            var masterInput = new FormSubmissionInputModel
            {
                FormId = header.MASTER_TABLE_ID,
                Pk = input.MasterPk,
                InputFields = input.MasterFields
            };
            _formService.SubmitForm(masterInput);

            foreach (var row in input.DetailRows)
            {
                var relField = row.Fields.FirstOrDefault(f => f.FieldConfigId == detailCfgId);
                if (relField == null)
                {
                    row.Fields.Add(new FormInputField
                    {
                        FieldConfigId = detailCfgId,
                        Value = relationValue
                    });
                }
                else
                {
                    relField.Value = relationValue;
                }

                var detailInput = new FormSubmissionInputModel
                {
                    FormId = header.DETAIL_TABLE_ID,
                    Pk = row.Pk,
                    InputFields = row.Fields
                };
                _formService.SubmitForm(detailInput);
            }
        });
    }
}
