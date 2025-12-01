using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.RouteOperation.Mappers;

public class ConditionMapper
{
    public static BAS_CONDITION MapperCreate(CreateConditionRequest request)
    {
        return new BAS_CONDITION
        {
            SID = RandomHelper.GenerateRandomDecimal(), 
            CONDITION_CODE = request.ConditionCode,
            LEFT_EXPRESSION = request.LeftExpression,
            OPERATOR = request.Operator,
            RIGHT_VALUE = request.RightValue
        };
    }

    public static void MapperUpdate(BAS_CONDITION entity, UpdateConditionRequest request)
    {
        if (request.ConditionCode != null)
            entity.CONDITION_CODE = request.ConditionCode;

        if (request.LeftExpression != null)
            entity.LEFT_EXPRESSION = request.LeftExpression;

        if (request.Operator != null)
            entity.OPERATOR = request.Operator;

        if (request.RightValue != null)
            entity.RIGHT_VALUE = request.RightValue;
    }

    public static ConditionViewModel ToViewModel(BAS_CONDITION entity)
    {
        return new ConditionViewModel
        {
            Sid = entity.SID,
            ConditionCode = entity.CONDITION_CODE,
            LeftExpression = entity.LEFT_EXPRESSION,
            Operator = entity.OPERATOR,
            RightValue = entity.RIGHT_VALUE
        };
    }
}