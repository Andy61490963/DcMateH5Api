using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.RouteOperation.Mappers;

public class OperationMapper
{
    public static BAS_OPERATION MapperCreate(CreateOperationRequest request)
    {
        return new BAS_OPERATION
        {
            SID = RandomHelper.GenerateRandomDecimal(), 
            OPERATION_TYPE = request.OperationType,
            OPERATION_CODE = request.OperationCode,
            OPERATION_NAME = request.OperationName
        };
    }

    public static void MapperUpdate(BAS_OPERATION entity, UpdateOperationRequest request)
    {
        if (request.OperationType != null)
            entity.OPERATION_TYPE = request.OperationType;

        if (request.OperationCode != null)
            entity.OPERATION_CODE = request.OperationCode;

        if (request.OperationName != null)
            entity.OPERATION_NAME = request.OperationName;
    }

    public static OperationViewModel ToViewModel(BAS_OPERATION entity)
    {
        return new OperationViewModel
        {
            Sid = entity.SID,
            OperationType = entity.OPERATION_TYPE,
            OperationCode = entity.OPERATION_CODE,
            OperationName = entity.OPERATION_NAME
        };
    }
}