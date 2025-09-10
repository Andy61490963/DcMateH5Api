using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace DcMateH5Api.Areas.Form.Interfaces.FormLogic;

public interface IFormFieldMasterService
{
    FormFieldMasterDto? GetFormFieldMaster(TableSchemaQueryType type);

    FormFieldMasterDto GetFormFieldMasterFromId(Guid? id, SqlTransaction? tx = null );

    List<(FormFieldMasterDto Master, List<FormFieldConfigDto> FieldConfigs)> GetFormMetaAggregates(
        FormFunctionType funcType, TableSchemaQueryType type);
}