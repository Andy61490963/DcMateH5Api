using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

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

    public FormMasterDetailService(
        SqlConnection connection,
        IFormService formService,
        IFormFieldMasterService formFieldMasterService,
        IFormDataService formDataService,
        ISchemaService schemaService,
        ITransactionService txService)
    {
        _con = connection;
        _formService = formService;
        _formFieldMasterService = formFieldMasterService;
        _formDataService = formDataService;
        _schemaService = schemaService;
        _txService = txService;
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

        // 取得主表主鍵欄位名稱，同時做為明細表關聯欄位
        var relationColumn = _schemaService.GetPrimaryKeyColumn(header.MASTER_TABLE_NAME);
        if (relationColumn == null)
            throw new InvalidOperationException("Master table has no primary key.");

        // 取得明細表主鍵欄位名稱
        var detailPk = _schemaService.GetPrimaryKeyColumn(header.DETAIL_TABLE_NAME);
        if (detailPk == null)
            throw new InvalidOperationException("Detail table has no primary key.");

        // 先撈出所有符合關聯欄位值的明細資料列
        var rows = _formDataService.GetRows(
            header.DETAIL_TABLE_NAME,
            new List<FormQueryCondition>
            {
                new FormQueryCondition
                {
                    Column = relationColumn,
                    ConditionType = ConditionType.Equal,
                    Value = pk,
                    DataType = "string"
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
            // 取得主明細表頭
            var header = _formFieldMasterService.GetFormFieldMasterFromId(input.FormId, tx);

            // 取得關聯欄位在主表與明細表的 FieldConfig ID
            var masterCfgId = _con.ExecuteScalar<Guid?>(
                "SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @Id AND COLUMN_NAME = @Col",
                new { Id = header.MASTER_TABLE_ID, Col = input.RelationColumn }, transaction: tx)
                ?? throw new InvalidOperationException("Master relation column not found.");
            var detailCfgId = _con.ExecuteScalar<Guid?>(
                "SELECT ID FROM FORM_FIELD_CONFIG WHERE FORM_FIELD_Master_ID = @Id AND COLUMN_NAME = @Col",
                new { Id = header.DETAIL_TABLE_ID, Col = input.RelationColumn }, transaction: tx)
                ?? throw new InvalidOperationException("Detail relation column not found.");

            // 將關聯欄位補入主表資料後提交
            input.MasterFields.Add(new FormInputField
            {
                FieldConfigId = masterCfgId,
                Value = input.RelationValue
            });

            var masterInput = new FormSubmissionInputModel
            {
                FormId = header.MASTER_TABLE_ID,
                Pk = input.MasterPk,
                InputFields = input.MasterFields
            };
            _formService.SubmitForm(masterInput);

            // 明細資料逐筆處理
            foreach (var row in input.DetailRows)
            {
                row.Fields.Add(new FormInputField
                {
                    FieldConfigId = detailCfgId,
                    Value = input.RelationValue
                });

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
