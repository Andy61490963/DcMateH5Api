using DcMateH5Api.Areas.RouteOperation.Models;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.RouteOperation.Mappers;

public static class RouteOperationMapper
    {
        /// <summary>
        /// 一般對應表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static BAS_ROUTE_OPERATION MapperCreateRouteOperation(CreateRouteOperationRequest request)
        {
            return new BAS_ROUTE_OPERATION
            {
                SID = RandomHelper.GenerateRandomDecimal(), 
                BAS_ROUTE_SID = request.RouteSid,
                BAS_OPERATION_SID = request.OperationSid,
                SEQ = request.Seq,
                ERP_STAGE = request.ErpStage,
                END_FLAG = request.EndFlag
            };
        }

        /// <summary>
        /// Extra對應表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static BAS_ROUTE_OPERATION_EXTRA MapperCreateRouteExtraOperation(CreateRouteExtraOperationRequest request)
        {
            return new BAS_ROUTE_OPERATION_EXTRA
            {
                SID = RandomHelper.GenerateRandomDecimal(), 
                BAS_ROUTE_SID = request.RouteSid,
                BAS_OPERATION_SID = request.OperationSid,
            };
        }
        
        public static void MapperUpdateRouteOperation(BAS_ROUTE_OPERATION entity, UpdateRouteOperationRequest request)
        {
            if (request.Seq.HasValue)
            {
                entity.SEQ = request.Seq.Value;
            }

            if (request.ErpStage != null)
            {
                entity.ERP_STAGE = request.ErpStage;
            }

            if (request.EndFlag.HasValue)
            {
                entity.END_FLAG = request.EndFlag.Value;
            }
        }
        
        public static BAS_ROUTE_OPERATION_CONDITION MapperCreateCondition(CreateRouteOperationConditionRequest request)
        {
            return new BAS_ROUTE_OPERATION_CONDITION
            {
                SID = RandomHelper.GenerateRandomDecimal(), 
                BAS_ROUTE_OPERATION_SID = request.RouteOperationSid,
                BAS_CONDITION_SID = request.ConditionSid,
                NEXT_ROUTE_OPERATION_SID = request.NextRouteOperationSid,
                NEXT_ROUTE_EXTRA_OPERATION_SID = request.NextRouteExtraOperationSid,
                HOLD = request.Hold,
                SEQ = request.Seq
            };
        }

        public static void MapperUpdateCondition(BAS_ROUTE_OPERATION_CONDITION entity, UpdateRouteOperationConditionRequest request)
        {
            if (request.Seq.HasValue)
            {
                entity.SEQ = request.Seq.Value;
            }

            if (request.ConditionSid.HasValue)
            {
                entity.BAS_CONDITION_SID = request.ConditionSid.Value;
            }

            if (request.NextRouteOperationSid.HasValue)
            {
                entity.NEXT_ROUTE_OPERATION_SID = request.NextRouteOperationSid.Value;
            }

            if (request.NextRouteExtraOperationSid.HasValue)
            {
                entity.NEXT_ROUTE_EXTRA_OPERATION_SID = request.NextRouteExtraOperationSid.Value;
            }

            if (request.Hold != null)
            {
                entity.HOLD = request.Hold;
            }
        }
    }