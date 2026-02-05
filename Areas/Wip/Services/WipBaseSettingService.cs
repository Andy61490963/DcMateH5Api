using DcMateH5Api.SqlHelper;
using DcMateH5Api.Areas.Wip.Interfaces;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Wip.Services;


public class WipBaseSettingService : IWipBaseSettingService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IBaseInfoCheckExistService _baseInfoCheckExistService;

    public WipBaseSettingService(
        SQLGenerateHelper sqlHelper,
        IBaseInfoCheckExistService baseInfoCheckExistService)
    {
        _sqlHelper = sqlHelper;
        _baseInfoCheckExistService = baseInfoCheckExistService;
    }
    
    public async Task CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default)
    {
        // 1. Validation
        
        // Account (List, Nullable)
        var userSids = new List<decimal>();
        if (input.Account != null && input.Account.Any())
        {
            foreach (var accountNo in input.Account)
            {
                var user = await _baseInfoCheckExistService.CheckUserExistAsync(accountNo, ct);
                if (user == null)
                {
                    throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Account {accountNo} does not exist.");
                }
                userSids.Add(user.USER_SID);
            }
        }

        // Equipment (List, Nullable)
        if (input.Equipment != null && input.Equipment.Any())
        {
            foreach (var eqpNo in input.Equipment)
            {
                var eqp = await _baseInfoCheckExistService.CheckEquipmentExistAsync(eqpNo, ct);
                if (eqp == null)
                {
                    throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Equipment {eqpNo} does not exist.");
                }
            }
        }
        
        // Work Order
        var wo = await _baseInfoCheckExistService.CheckWorkOrderExistAsync(input.WorkOrder, ct);
        if (wo == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Work Order {input.WorkOrder} does not exist.");
        }

        // Operation
        var operation = await _baseInfoCheckExistService.CheckOperationExistAsync(input.Operation, ct);
        if (operation == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Operation {input.Operation} does not exist.");
        }

        // Department
        var dept = await _baseInfoCheckExistService.CheckDepartmentExistAsync(input.Department, ct);
        if (dept == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Department {input.Department} does not exist.");
        }

        // 2. Duplicate Check (WIP_OPI_WDOEACICO_HIST)
        // Check if there is already a record with same WO, same Equipments (if any) and same CheckInTime
        if (input.Equipment != null && input.Equipment.Any())
        {
            foreach (var eqpNo in input.Equipment)
            {
                var dupWhere = new WhereBuilder<WipOpiWdoeacicoHistDto>()
                    .AndEq(x => x.WO!, input.WorkOrder)
                    .AndEq(x => x.EQP_NO!, eqpNo)
                    .AndEq(x => x.CHECK_IN_TIME!, input.CheckInTime);

                var duplicate = await _sqlHelper.SelectFirstOrDefaultAsync(dupWhere, ct);
                if (duplicate != null)
                {
                     throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Duplicate check-in found for WO: {input.WorkOrder}, Equipment: {eqpNo} at Time: {input.CheckInTime}");
                }
            }
        }
        else
        {
             var dupWhere = new WhereBuilder<WipOpiWdoeacicoHistDto>()
                    .AndEq(x => x.WO!, input.WorkOrder)
                    .AndEq(x => x.CHECK_IN_TIME!, input.CheckInTime);
             
             var duplicate = await _sqlHelper.SelectFirstOrDefaultAsync(dupWhere, ct);
              if (duplicate != null)
              {
                   // Further filtering if EQP_NO in DB is null and input is null
                   if(string.IsNullOrEmpty(duplicate.EQP_NO))
                   {
                        throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Duplicate check-in found for WO: {input.WorkOrder} at Time: {input.CheckInTime} (No Equipment)");
                   }
              }
        }

        // 3. Persistence
        var histSid = RandomHelper.GenerateRandomDecimal();
        
        var hist = new WipOpiWdoeacicoHistDto
        {
            WIP_OPI_WDOEACICO_HIST_SID = histSid,
            WO = input.WorkOrder,
            CHECK_IN_TIME = input.CheckInTime,
            OPERATION_CODE = input.Operation,
            DEPT_NO = input.Department,
            COMMENT = input.Comment,
            COMPLETED = "N"
        };

        // Equipment Handling
        if (input.Equipment != null && input.Equipment.Count == 1)
        {
            hist.EQP_NO = input.Equipment[0];
        }
        
        await _sqlHelper.InsertAsync(hist, ct);

        // Multiple Equipments
        if (input.Equipment != null && input.Equipment.Count > 1)
        {
            foreach (var eqpNo in input.Equipment)
            {
                var histEqp = new WipOpiWdoeacicoHistEqpDto
                {
                    WIP_OPI_WDOEACICO_HIST_EQP_SID = RandomHelper.GenerateRandomDecimal(),
                    WIP_OPI_WDOEACICO_HIST_SID = histSid,
                    EQP_NO = eqpNo,
                    ENABLE_FLAG = "Y" // Assuming 'Y' for enable flag
                };
                await _sqlHelper.InsertAsync(histEqp, ct);
            }
        }

        // Multiple USER
        if (input.Account != null && input.Account.Any())
        {
             for (int i = 0; i < input.Account.Count; i++)
             {
                 var histUser = new WipOpiWdoeacicoHistUserDto
                 {
                     WIP_OPI_WDOEACICO_HIST_USER_SID = RandomHelper.GenerateRandomDecimal(),
                     WIP_OPI_WDOEACICO_HIST_SID = histSid,
                     UMM_USER_SID = userSids[i],
                     ACCOUNT_NO = input.Account[i]
                 };
                 await _sqlHelper.InsertAsync(histUser, ct);
             }
        }
    }
}