using System.Data;
using System.Net;
using DbExtensions.DbExecutor.Interface;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Prj;
using DcMateH5.Abstractions.Prj.Models;
using DcMateClassLibrary.Models;

namespace DcMateH5.Infrastructure.Prj;

public sealed class PrjService : IPrjService
{
    private static readonly DateTime EmptyDate = new(1900, 1, 1);
    private readonly IDbExecutor _db;
    private readonly ICurrentUserAccessor _currentUser;

    public PrjService(IDbExecutor db, ICurrentUserAccessor currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PrjProjectListItemDto>> GetProjectsAsync(PrjProjectQuery query, CancellationToken ct = default)
    {
        ValidatePage(query.Page, query.PageSize);
        ValidateDateRange(query.StartFrom, query.StartTo, "專案開始日期");

        var orderBy = ResolveProjectSort(query.SortBy);
        var direction = query.SortDescending ? "DESC" : "ASC";
        var param = new
        {
            Keyword = TrimToNull(query.Keyword),
            query.StatusNo,
            query.TypeNo,
            CustomerNo = TrimToNull(query.CustomerNo),
            EnableFlag = ToFlag(query.Enabled),
            query.StartFrom,
            query.StartTo,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize
        };

        var total = await _db.ExecuteScalarAsync<int>(ProjectCountSql, param, ct: ct);
        var rows = await _db.QueryAsync<PrjProjectListItemDto>(
            $"{ProjectSelectSql} ORDER BY {orderBy} {direction}, m.PROJECT_CODE ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            param,
            ct: ct);

        return new PagedResult<PrjProjectListItemDto>
        {
            Items = rows,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<PrjProjectDto> GetProjectAsync(string projectCode, CancellationToken ct = default)
    {
        var code = RequireText(projectCode, "PROJECT_CODE");
        var row = await _db.QueryFirstOrDefaultAsync<PrjProjectDto>(
            $"{ProjectDetailSelectSql} WHERE m.PROJECT_CODE = @ProjectCode;",
            new { ProjectCode = code },
            ct: ct);
        return row ?? throw NotFound($"找不到專案：{code}");
    }

    public async Task<PrjProjectDto> CreateProjectAsync(CreatePrjProjectRequest request, CancellationToken ct = default)
    {
        var code = RequireText(request.ProjectCode, "PROJECT_CODE");
        ValidateDateRange(request.StartTime, request.ExpectedTime, "專案起訖日期");
        ValidateDateRange(request.StartTime, request.EndTime, "專案實際日期");
        var user = GetUserAccount();

        await _db.TxAsync(async (conn, tx, token) =>
        {
            var exists = await _db.ExecuteScalarInTxAsync<int>(conn, tx,
                "SELECT COUNT(1) FROM PRJ_MASTER WITH (UPDLOCK, HOLDLOCK) WHERE PROJECT_CODE = @ProjectCode;",
                new { ProjectCode = code }, ct: token);
            if (exists > 0) throw Conflict($"專案代碼已存在：{code}");

            await ValidateProjectReferencesAsync(conn, tx, request.StatusNo, request.TypeNo, request.CustomerNo, token);
            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                INSERT INTO PRJ_MASTER
                (PRJ_MASTER_SID, SEQ, PROJECT_CODE, PROJECT_NAME, PROJECT_MAINTAIN_STATUS, TYPE_NO,
                 CUSTOMER_NO, START_TIME, EXPECTED_TIME, END_TIME, IS_ORDER, ENABLE_FLAG,
                 CREATE_USER, CREATE_TIME, EDIT_USER, EDIT_TIME)
                VALUES
                (@ProjectSid, @Seq, @ProjectCode, @ProjectName, @StatusNo, @TypeNo,
                 @CustomerNo, @StartTime, @ExpectedTime, @EndTime, @IsOrder, 'Y',
                 @User, @Now, @User, @Now);
                """, new
            {
                ProjectSid = RandomHelper.GenerateRandomDecimal(),
                request.Seq,
                ProjectCode = code,
                ProjectName = TrimToNull(request.ProjectName),
                request.StatusNo,
                request.TypeNo,
                CustomerNo = TrimToNull(request.CustomerNo),
                request.StartTime,
                request.ExpectedTime,
                request.EndTime,
                IsOrder = TrimToNull(request.IsOrder),
                User = user,
                Now = now
            }, ct: token);
            if (affected != 1) throw Conflict($"建立專案失敗：{code}");
        }, IsolationLevel.Serializable, ct);

        return await GetProjectAsync(code, ct);
    }

    public async Task<PrjProjectDto> UpdateProjectAsync(string projectCode, UpdatePrjProjectRequest request, CancellationToken ct = default)
    {
        var code = RequireText(projectCode, "PROJECT_CODE");
        var editTime = RequireEditTime(request.EditTime);
        ValidateDateRange(request.StartTime, request.ExpectedTime, "專案起訖日期");
        ValidateDateRange(request.StartTime, request.EndTime, "專案實際日期");
        var user = GetUserAccount();

        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureProjectExistsAsync(conn, tx, code, token);
            await ValidateProjectReferencesAsync(conn, tx, request.StatusNo, request.TypeNo, request.CustomerNo, token);
            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                UPDATE PRJ_MASTER
                   SET SEQ = @Seq,
                       PROJECT_NAME = @ProjectName,
                       PROJECT_MAINTAIN_STATUS = @StatusNo,
                       TYPE_NO = @TypeNo,
                       CUSTOMER_NO = @CustomerNo,
                       START_TIME = @StartTime,
                       EXPECTED_TIME = @ExpectedTime,
                       END_TIME = @EndTime,
                       IS_ORDER = @IsOrder,
                       EDIT_USER = @User,
                       EDIT_TIME = @Now
                 WHERE PROJECT_CODE = @ProjectCode AND EDIT_TIME = @OriginalEditTime;
                """, new
            {
                request.Seq,
                ProjectName = TrimToNull(request.ProjectName),
                request.StatusNo,
                request.TypeNo,
                CustomerNo = TrimToNull(request.CustomerNo),
                request.StartTime,
                request.ExpectedTime,
                request.EndTime,
                IsOrder = TrimToNull(request.IsOrder),
                User = user,
                Now = now,
                ProjectCode = code,
                OriginalEditTime = editTime
            }, ct: token);
            if (affected != 1) throw Conflict("專案已被其他使用者修改，請重新載入後再試。");
        }, ct: ct);

        return await GetProjectAsync(code, ct);
    }

    public async Task<PrjProjectDto> ChangeProjectEnabledAsync(string projectCode, ChangeEnabledRequest request, CancellationToken ct = default)
    {
        var code = RequireText(projectCode, "PROJECT_CODE");
        var editTime = RequireEditTime(request.EditTime);
        var user = GetUserAccount();

        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureProjectExistsAsync(conn, tx, code, token);
            if (!request.Enabled)
            {
                var activeDetails = await _db.ExecuteScalarInTxAsync<int>(conn, tx,
                    "SELECT COUNT(1) FROM PRJ_DETAIL WHERE PROJECT_CODE = @ProjectCode AND ENABLE_FLAG = 'Y';",
                    new { ProjectCode = code }, ct: token);
                if (activeDetails > 0) throw Conflict("專案仍有啟用中的工作明細，請先停用明細。");
            }

            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                UPDATE PRJ_MASTER
                   SET ENABLE_FLAG = @EnableFlag, EDIT_USER = @User, EDIT_TIME = @Now
                 WHERE PROJECT_CODE = @ProjectCode AND EDIT_TIME = @OriginalEditTime;
                """, new
            {
                EnableFlag = request.Enabled ? "Y" : "N",
                User = user,
                Now = now,
                ProjectCode = code,
                OriginalEditTime = editTime
            }, ct: token);
            if (affected != 1) throw Conflict("專案已被其他使用者修改，請重新載入後再試。");
        }, ct: ct);

        return await GetProjectAsync(code, ct);
    }

    public async Task ReorderProjectsAsync(ReorderPrjProjectsRequest request, CancellationToken ct = default)
    {
        EnsureUnique(request.Items.Select(x => x.ProjectCode), "專案代碼");
        var user = GetUserAccount();
        await _db.TxAsync(async (conn, tx, token) =>
        {
            foreach (var item in request.Items)
            {
                var now = await GetDbNowAsync(conn, tx, token);
                var affected = await _db.ExecuteInTxAsync(conn, tx, """
                    UPDATE PRJ_MASTER SET SEQ = @Seq, EDIT_USER = @User, EDIT_TIME = @Now
                     WHERE PROJECT_CODE = @ProjectCode AND EDIT_TIME = @OriginalEditTime;
                    """, new
                {
                    item.Seq,
                    User = user,
                    Now = now,
                    ProjectCode = RequireText(item.ProjectCode, "PROJECT_CODE"),
                    OriginalEditTime = RequireEditTime(item.EditTime)
                }, ct: token);
                if (affected != 1) throw Conflict($"專案排序資料已變更：{item.ProjectCode}");
            }
        }, ct: ct);
    }

    public async Task<PagedResult<PrjDetailDto>> GetDetailsAsync(string projectCode, PrjDetailQuery query, CancellationToken ct = default)
    {
        var code = RequireText(projectCode, "PROJECT_CODE");
        ValidatePage(query.Page, query.PageSize);
        ValidateDateRange(query.StartFrom, query.EndTo, "工作日期");
        await EnsureProjectExistsAsync(code, ct);
        var orderBy = ResolveDetailSort(query.SortBy);
        var direction = query.SortDescending ? "DESC" : "ASC";
        var param = new
        {
            ProjectCode = code,
            Keyword = TrimToNull(query.Keyword),
            query.StatusNo,
            query.ProcessTypeNo,
            UserAccount = TrimToNull(query.UserAccount),
            EnableFlag = ToFlag(query.Enabled),
            query.StartFrom,
            query.EndTo,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize
        };
        var total = await _db.ExecuteScalarAsync<int>(DetailCountSql, param, ct: ct);
        var rows = await _db.QueryAsync<PrjDetailDto>(
            $"{DetailSelectSql} {DetailWhereSql} ORDER BY {orderBy} {direction}, d.PRJ_DETAIL_SID ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            param, ct: ct);
        return new PagedResult<PrjDetailDto> { Items = rows, Page = query.Page, PageSize = query.PageSize, TotalCount = total };
    }

    public async Task<PrjDetailDto> GetDetailAsync(decimal detailSid, CancellationToken ct = default)
    {
        var row = await _db.QueryFirstOrDefaultAsync<PrjDetailDto>(
            $"{DetailSelectSql} WHERE d.PRJ_DETAIL_SID = @DetailSid;", new { DetailSid = detailSid }, ct: ct);
        return row ?? throw NotFound($"找不到工作明細：{detailSid}");
    }

    public async Task<PrjDetailDto> CreateDetailAsync(string projectCode, CreatePrjDetailRequest request, CancellationToken ct = default)
    {
        var code = RequireText(projectCode, "PROJECT_CODE");
        ValidateDetailDates(request.StartExpectedTime, request.StartTime, request.ExpectedTime, request.EndTime);
        var user = GetUserAccount();
        var sid = RandomHelper.GenerateRandomDecimal();
        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureProjectExistsAsync(conn, tx, code, token);
            await ValidateDetailReferencesAsync(conn, tx, request.ProcessTypeNo, request.StatusNo,
                request.PrincipalUser, request.SupportUser, request.ReviewerUser, token);
            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                INSERT INTO PRJ_DETAIL
                (PRJ_DETAIL_SID, PROJECT_CODE, PROCESS_TYPE, SUMMARY, PROJECT_STATUS, COMMENT,
                 PRINCIPAL_USER, SUPPORT_USER, REVIEWER_USER, START_EXPECTED_TIME, START_TIME,
                 EXPECTED_TIME, END_TIME, SEQ, ENABLE_FLAG, CREATE_USER, CREATE_TIME, EDIT_USER, EDIT_TIME, FILE_NAME)
                VALUES
                (@DetailSid, @ProjectCode, @ProcessTypeNo, @Summary, @StatusNo, @Comment,
                 @PrincipalUser, @SupportUser, @ReviewerUser, @StartExpectedTime, @StartTime,
                 @ExpectedTime, @EndTime, @Seq, 'Y', @User, @Now, @User, @Now, @FileName);
                """, new
            {
                DetailSid = sid,
                ProjectCode = code,
                request.ProcessTypeNo,
                Summary = TrimToNull(request.Summary),
                request.StatusNo,
                Comment = TrimToNull(request.Comment),
                PrincipalUser = TrimToNull(request.PrincipalUser),
                SupportUser = TrimToNull(request.SupportUser),
                ReviewerUser = TrimToNull(request.ReviewerUser),
                request.StartExpectedTime,
                request.StartTime,
                request.ExpectedTime,
                request.EndTime,
                request.Seq,
                User = user,
                Now = now,
                FileName = TrimToNull(request.FileName)
            }, ct: token);
            if (affected != 1) throw Conflict("建立工作明細失敗。");
        }, ct: ct);
        return await GetDetailAsync(sid, ct);
    }

    public async Task<PrjDetailDto> UpdateDetailAsync(decimal detailSid, UpdatePrjDetailRequest request, CancellationToken ct = default)
    {
        var editTime = RequireEditTime(request.EditTime);
        ValidateDetailDates(request.StartExpectedTime, request.StartTime, request.ExpectedTime, request.EndTime);
        var user = GetUserAccount();
        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureDetailExistsAsync(conn, tx, detailSid, token);
            await ValidateDetailReferencesAsync(conn, tx, request.ProcessTypeNo, request.StatusNo,
                request.PrincipalUser, request.SupportUser, request.ReviewerUser, token);
            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                UPDATE PRJ_DETAIL SET
                    PROCESS_TYPE = @ProcessTypeNo, SUMMARY = @Summary, PROJECT_STATUS = @StatusNo,
                    COMMENT = @Comment, PRINCIPAL_USER = @PrincipalUser, SUPPORT_USER = @SupportUser,
                    REVIEWER_USER = @ReviewerUser, START_EXPECTED_TIME = @StartExpectedTime,
                    START_TIME = @StartTime, EXPECTED_TIME = @ExpectedTime, END_TIME = @EndTime,
                    SEQ = @Seq, FILE_NAME = @FileName, EDIT_USER = @User, EDIT_TIME = @Now
                 WHERE PRJ_DETAIL_SID = @DetailSid AND EDIT_TIME = @OriginalEditTime;
                """, new
            {
                request.ProcessTypeNo,
                Summary = TrimToNull(request.Summary),
                request.StatusNo,
                Comment = TrimToNull(request.Comment),
                PrincipalUser = TrimToNull(request.PrincipalUser),
                SupportUser = TrimToNull(request.SupportUser),
                ReviewerUser = TrimToNull(request.ReviewerUser),
                request.StartExpectedTime,
                request.StartTime,
                request.ExpectedTime,
                request.EndTime,
                request.Seq,
                FileName = TrimToNull(request.FileName),
                User = user,
                Now = now,
                DetailSid = detailSid,
                OriginalEditTime = editTime
            }, ct: token);
            if (affected != 1) throw Conflict("工作明細已被其他使用者修改，請重新載入後再試。");
        }, ct: ct);
        return await GetDetailAsync(detailSid, ct);
    }

    public async Task<PrjDetailDto> ChangeDetailStatusAsync(decimal detailSid, ChangePrjDetailStatusRequest request, CancellationToken ct = default)
    {
        var statusNo = request.StatusNo ?? throw BadRequest("STATUS_NO 為必填欄位。");
        var editTime = RequireEditTime(request.EditTime);
        var user = GetUserAccount();
        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureDetailExistsAsync(conn, tx, detailSid, token);
            await EnsureCodeExistsAsync(conn, tx, "PRJ_DETAIL_STATUS", "PROJECT_STATUS_NO", statusNo, "工作狀態", token);
            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                UPDATE PRJ_DETAIL SET PROJECT_STATUS = @StatusNo, EDIT_USER = @User, EDIT_TIME = @Now
                 WHERE PRJ_DETAIL_SID = @DetailSid AND EDIT_TIME = @OriginalEditTime;
                """, new { StatusNo = statusNo, User = user, Now = now, DetailSid = detailSid, OriginalEditTime = editTime }, ct: token);
            if (affected != 1) throw Conflict("工作明細已被其他使用者修改，請重新載入後再試。");
        }, ct: ct);
        return await GetDetailAsync(detailSid, ct);
    }

    public async Task<PrjDetailDto> ChangeDetailEnabledAsync(decimal detailSid, ChangeEnabledRequest request, CancellationToken ct = default)
    {
        var editTime = RequireEditTime(request.EditTime);
        var user = GetUserAccount();
        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureDetailExistsAsync(conn, tx, detailSid, token);
            var now = await GetDbNowAsync(conn, tx, token);
            var affected = await _db.ExecuteInTxAsync(conn, tx, """
                UPDATE PRJ_DETAIL SET ENABLE_FLAG = @EnableFlag, EDIT_USER = @User, EDIT_TIME = @Now
                 WHERE PRJ_DETAIL_SID = @DetailSid AND EDIT_TIME = @OriginalEditTime;
                """, new
            {
                EnableFlag = request.Enabled ? "Y" : "N", User = user, Now = now,
                DetailSid = detailSid, OriginalEditTime = editTime
            }, ct: token);
            if (affected != 1) throw Conflict("工作明細已被其他使用者修改，請重新載入後再試。");
        }, ct: ct);
        return await GetDetailAsync(detailSid, ct);
    }

    public async Task ReorderDetailsAsync(string projectCode, ReorderPrjDetailsRequest request, CancellationToken ct = default)
    {
        var code = RequireText(projectCode, "PROJECT_CODE");
        EnsureUnique(request.Items.Select(x => x.DetailSid.ToString()), "工作明細 SID");
        var user = GetUserAccount();
        await _db.TxAsync(async (conn, tx, token) =>
        {
            await EnsureProjectExistsAsync(conn, tx, code, token);
            foreach (var item in request.Items)
            {
                var now = await GetDbNowAsync(conn, tx, token);
                var affected = await _db.ExecuteInTxAsync(conn, tx, """
                    UPDATE PRJ_DETAIL SET SEQ = @Seq, EDIT_USER = @User, EDIT_TIME = @Now
                     WHERE PRJ_DETAIL_SID = @DetailSid AND PROJECT_CODE = @ProjectCode AND EDIT_TIME = @OriginalEditTime;
                    """, new
                {
                    item.Seq, User = user, Now = now, item.DetailSid, ProjectCode = code,
                    OriginalEditTime = RequireEditTime(item.EditTime)
                }, ct: token);
                if (affected != 1) throw Conflict($"工作明細排序資料已變更：{item.DetailSid}");
            }
        }, ct: ct);
    }

    public async Task<PrjLookupOptionsDto> GetOptionsAsync(CancellationToken ct = default)
    {
        var projectStatuses = await _db.QueryAsync<PrjOptionDto>(
            "SELECT PROJECT_MAINTAIN_STATUS_NO AS Value, PROJECT_MAINTAIN_STATUS_NAME AS Text, CAST(NULL AS bit) AS IsCompleted FROM PRJ_MASTER_STATUS ORDER BY PROJECT_MAINTAIN_STATUS_NO;", ct: ct);
        var projectTypes = await _db.QueryAsync<PrjOptionDto>(
            "SELECT TYPE_NO AS Value, TYPE_NAME AS Text, CAST(NULL AS bit) AS IsCompleted FROM PRJ_MASTER_TYPE ORDER BY TYPE_NO;", ct: ct);
        var detailStatuses = await _db.QueryAsync<PrjOptionDto>(
            "SELECT PROJECT_STATUS_NO AS Value, PROJECT_STATUS_NAME AS Text, CAST(CASE WHEN PROJECT_STATUS_FLAG = 'Y' THEN 1 ELSE 0 END AS bit) AS IsCompleted FROM PRJ_DETAIL_STATUS ORDER BY PROJECT_STATUS_NO;", ct: ct);
        var processTypes = await _db.QueryAsync<PrjOptionDto>(
            "SELECT PROCESS_TYPE AS Value, PROCESS_NAME AS Text, CAST(NULL AS bit) AS IsCompleted FROM PRJ_DETAIL_PROCESS_TYPE ORDER BY PROCESS_TYPE;", ct: ct);
        return new PrjLookupOptionsDto
        {
            ProjectStatuses = projectStatuses,
            ProjectTypes = projectTypes,
            DetailStatuses = detailStatuses,
            ProcessTypes = processTypes
        };
    }

    public async Task<IReadOnlyList<PrjTextOptionDto>> GetCustomersAsync(string? keyword, int take, CancellationToken ct = default)
    {
        ValidateTake(take);
        return await _db.QueryAsync<PrjTextOptionDto>("""
            SELECT TOP (@Take) CUSTOMER_NO AS Value,
                   COALESCE(NULLIF(CUSTOMER_SHORT_NAME, ''), NULLIF(CUSTOMER_NAME, ''), CUSTOMER_NO) AS Text
              FROM ADM_CUSTOMER
             WHERE ENABLE_FLAG = 'Y'
               AND (@Keyword IS NULL OR CUSTOMER_NO LIKE '%' + @Keyword + '%'
                    OR CUSTOMER_NAME LIKE '%' + @Keyword + '%'
                    OR CUSTOMER_SHORT_NAME LIKE '%' + @Keyword + '%')
             ORDER BY CUSTOMER_NO;
            """, new { Keyword = TrimToNull(keyword), Take = take }, ct: ct);
    }

    public async Task<IReadOnlyList<PrjTextOptionDto>> GetUsersAsync(string? keyword, int take, CancellationToken ct = default)
    {
        ValidateTake(take);
        return await _db.QueryAsync<PrjTextOptionDto>("""
            SELECT TOP (@Take) ACCOUNT_NO AS Value, COALESCE(NULLIF(USER_NAME, ''), ACCOUNT_NO) AS Text
              FROM ADM_USER
             WHERE ENABLE_FLAG = 'Y'
               AND (@Keyword IS NULL OR ACCOUNT_NO LIKE '%' + @Keyword + '%' OR USER_NAME LIKE '%' + @Keyword + '%')
             ORDER BY ACCOUNT_NO;
            """, new { Keyword = TrimToNull(keyword), Take = take }, ct: ct);
    }

    private string GetUserAccount()
    {
        var current = _currentUser.Get();
        if (!current.IsAuthenticated || string.IsNullOrWhiteSpace(current.Account) || current.Account == CurrentUserSnapshot.NotLoginUser)
            return CurrentUserSnapshot.NotLoginUser;
        return current.Account.Trim();
    }

    private async Task ValidateProjectReferencesAsync(Microsoft.Data.SqlClient.SqlConnection conn, Microsoft.Data.SqlClient.SqlTransaction tx,
        decimal? statusNo, decimal? typeNo, string? customerNo, CancellationToken ct)
    {
        if (statusNo.HasValue) await EnsureCodeExistsAsync(conn, tx, "PRJ_MASTER_STATUS", "PROJECT_MAINTAIN_STATUS_NO", statusNo.Value, "專案狀態", ct);
        if (typeNo.HasValue) await EnsureCodeExistsAsync(conn, tx, "PRJ_MASTER_TYPE", "TYPE_NO", typeNo.Value, "專案類型", ct);
        var customer = TrimToNull(customerNo);
        if (customer != null)
        {
            var count = await _db.ExecuteScalarInTxAsync<int>(conn, tx,
                "SELECT COUNT(1) FROM ADM_CUSTOMER WHERE CUSTOMER_NO = @Value AND ENABLE_FLAG = 'Y';", new { Value = customer }, ct: ct);
            if (count == 0) throw BadRequest($"客戶不存在或已停用：{customer}");
        }
    }

    private async Task ValidateDetailReferencesAsync(Microsoft.Data.SqlClient.SqlConnection conn, Microsoft.Data.SqlClient.SqlTransaction tx,
        decimal? processTypeNo, decimal? statusNo, string? principalUser, string? supportUser, string? reviewerUser, CancellationToken ct)
    {
        if (processTypeNo.HasValue) await EnsureCodeExistsAsync(conn, tx, "PRJ_DETAIL_PROCESS_TYPE", "PROCESS_TYPE", processTypeNo.Value, "處理類型", ct);
        if (!statusNo.HasValue) throw BadRequest("STATUS_NO 為必填欄位。");
        await EnsureCodeExistsAsync(conn, tx, "PRJ_DETAIL_STATUS", "PROJECT_STATUS_NO", statusNo.Value, "工作狀態", ct);
        foreach (var account in new[] { TrimToNull(principalUser), TrimToNull(supportUser), TrimToNull(reviewerUser) }.Where(x => x != null).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var count = await _db.ExecuteScalarInTxAsync<int>(conn, tx,
                "SELECT COUNT(1) FROM ADM_USER WHERE ACCOUNT_NO = @Account AND ENABLE_FLAG = 'Y';", new { Account = account }, ct: ct);
            if (count == 0) throw BadRequest($"使用者不存在或已停用：{account}");
        }
    }

    private async Task EnsureCodeExistsAsync(Microsoft.Data.SqlClient.SqlConnection conn, Microsoft.Data.SqlClient.SqlTransaction tx,
        string table, string column, decimal value, string label, CancellationToken ct)
    {
        var sql = $"SELECT COUNT(1) FROM {table} WHERE {column} = @Value;";
        var count = await _db.ExecuteScalarInTxAsync<int>(conn, tx, sql, new { Value = value }, ct: ct);
        if (count == 0) throw BadRequest($"{label}不存在：{value}");
    }

    private Task EnsureProjectExistsAsync(string projectCode, CancellationToken ct) =>
        EnsureExistsAsync("SELECT COUNT(1) FROM PRJ_MASTER WHERE PROJECT_CODE = @Value;", projectCode, $"找不到專案：{projectCode}", ct);

    private async Task EnsureExistsAsync(string sql, object value, string message, CancellationToken ct)
    {
        var count = await _db.ExecuteScalarAsync<int>(sql, new { Value = value }, ct: ct);
        if (count == 0) throw NotFound(message);
    }

    private async Task EnsureProjectExistsAsync(Microsoft.Data.SqlClient.SqlConnection conn, Microsoft.Data.SqlClient.SqlTransaction tx, string projectCode, CancellationToken ct)
    {
        var count = await _db.ExecuteScalarInTxAsync<int>(conn, tx, "SELECT COUNT(1) FROM PRJ_MASTER WHERE PROJECT_CODE = @ProjectCode;", new { ProjectCode = projectCode }, ct: ct);
        if (count == 0) throw NotFound($"找不到專案：{projectCode}");
    }

    private async Task EnsureDetailExistsAsync(Microsoft.Data.SqlClient.SqlConnection conn, Microsoft.Data.SqlClient.SqlTransaction tx, decimal detailSid, CancellationToken ct)
    {
        var count = await _db.ExecuteScalarInTxAsync<int>(conn, tx, "SELECT COUNT(1) FROM PRJ_DETAIL WHERE PRJ_DETAIL_SID = @DetailSid;", new { DetailSid = detailSid }, ct: ct);
        if (count == 0) throw NotFound($"找不到工作明細：{detailSid}");
    }

    private async Task<DateTime> GetDbNowAsync(Microsoft.Data.SqlClient.SqlConnection conn, Microsoft.Data.SqlClient.SqlTransaction tx, CancellationToken ct) =>
        await _db.ExecuteScalarInTxAsync<DateTime>(conn, tx, "SELECT SYSDATETIME();", ct: ct);

    private static void ValidateDetailDates(DateTime? expectedStart, DateTime? start, DateTime? expectedEnd, DateTime? end)
    {
        ValidateDateRange(expectedStart, expectedEnd, "工作預計日期");
        ValidateDateRange(start, end, "工作實際日期");
    }

    private static void ValidateDateRange(DateTime? from, DateTime? to, string label)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date) throw BadRequest($"{label}的開始日期不可晚於結束日期。");
        if (from.HasValue && from.Value.Date <= EmptyDate) throw BadRequest($"{label}不可使用 1900-01-01。");
        if (to.HasValue && to.Value.Date <= EmptyDate) throw BadRequest($"{label}不可使用 1900-01-01。");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw BadRequest("Page 必須大於 0，PageSize 必須介於 1 到 100。");
    }

    private static void ValidateTake(int take)
    {
        if (take is < 1 or > 100) throw BadRequest("take 必須介於 1 到 100。");
    }

    private static string ResolveProjectSort(string sortBy) => (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "projectcode" => "m.PROJECT_CODE",
        "projectname" => "m.PROJECT_NAME",
        "starttime" => "m.START_TIME",
        "expectedtime" => "m.EXPECTED_TIME",
        "edittime" => "m.EDIT_TIME",
        "seq" or "" => "m.SEQ",
        _ => throw BadRequest($"不支援的專案排序欄位：{sortBy}")
    };

    private static string ResolveDetailSort(string sortBy) => (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "summary" => "d.SUMMARY",
        "starttime" => "d.START_TIME",
        "expectedtime" => "d.EXPECTED_TIME",
        "endtime" => "d.END_TIME",
        "edittime" => "d.EDIT_TIME",
        "seq" or "" => "d.SEQ",
        _ => throw BadRequest($"不支援的工作排序欄位：{sortBy}")
    };

    private static string? ToFlag(bool? value) => value.HasValue ? value.Value ? "Y" : "N" : null;
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string RequireText(string? value, string field) => TrimToNull(value) ?? throw BadRequest($"{field} 為必填欄位。");
    private static DateTime RequireEditTime(DateTime? value) => value ?? throw BadRequest("EDIT_TIME 為必填欄位。");

    private static void EnsureUnique(IEnumerable<string> values, string label)
    {
        var items = values.ToList();
        if (items.Count == 0) throw BadRequest("排序項目不可為空。");
        if (items.Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count) throw BadRequest($"{label}不可重複。");
    }

    private static HttpStatusCodeException BadRequest(string message) => new(HttpStatusCode.BadRequest, message);
    private static HttpStatusCodeException NotFound(string message) => new(HttpStatusCode.NotFound, message);
    private static HttpStatusCodeException Conflict(string message) => new(HttpStatusCode.Conflict, message);

    private const string ProjectWhereSql = """
        WHERE (@Keyword IS NULL OR m.PROJECT_CODE LIKE '%' + @Keyword + '%' OR m.PROJECT_NAME LIKE '%' + @Keyword + '%')
          AND (@StatusNo IS NULL OR TRY_CONVERT(decimal(18,0), m.PROJECT_MAINTAIN_STATUS) = @StatusNo)
          AND (@TypeNo IS NULL OR TRY_CONVERT(decimal(18,0), m.TYPE_NO) = @TypeNo)
          AND (@CustomerNo IS NULL OR m.CUSTOMER_NO = @CustomerNo)
          AND (@EnableFlag IS NULL OR m.ENABLE_FLAG = @EnableFlag)
          AND (@StartFrom IS NULL OR NULLIF(m.START_TIME, '19000101') >= @StartFrom)
          AND (@StartTo IS NULL OR NULLIF(m.START_TIME, '19000101') <= @StartTo)
        """;

    private const string ProjectJoinsSql = """
        LEFT JOIN PRJ_MASTER_STATUS ms ON ms.PROJECT_MAINTAIN_STATUS_NO = TRY_CONVERT(decimal(18,0), m.PROJECT_MAINTAIN_STATUS)
        LEFT JOIN PRJ_MASTER_TYPE mt ON mt.TYPE_NO = TRY_CONVERT(decimal(18,0), m.TYPE_NO)
        LEFT JOIN ADM_CUSTOMER c ON c.CUSTOMER_NO = m.CUSTOMER_NO
        OUTER APPLY (
            SELECT COUNT(1) AS DetailCount,
                   SUM(CASE WHEN ds.PROJECT_STATUS_FLAG = 'Y' THEN 1 ELSE 0 END) AS CompletedDetailCount,
                   SUM(CASE WHEN ds.PROJECT_STATUS_FLAG <> 'Y' AND NULLIF(d.EXPECTED_TIME, '19000101') < CAST(GETDATE() AS date) THEN 1 ELSE 0 END) AS OverdueDetailCount
              FROM PRJ_DETAIL d
              LEFT JOIN PRJ_DETAIL_STATUS ds ON ds.PROJECT_STATUS_NO = TRY_CONVERT(decimal(18,0), d.PROJECT_STATUS)
             WHERE d.PROJECT_CODE = m.PROJECT_CODE AND d.ENABLE_FLAG = 'Y'
        ) stats
        """;

    private const string ProjectColumnsSql = """
        m.PRJ_MASTER_SID AS ProjectSid, m.SEQ AS Seq, m.PROJECT_CODE AS ProjectCode,
        m.PROJECT_NAME AS ProjectName, TRY_CONVERT(decimal(18,0), m.PROJECT_MAINTAIN_STATUS) AS StatusNo,
        ms.PROJECT_MAINTAIN_STATUS_NAME AS StatusName, TRY_CONVERT(decimal(18,0), m.TYPE_NO) AS TypeNo,
        mt.TYPE_NAME AS TypeName, m.CUSTOMER_NO AS CustomerNo,
        COALESCE(NULLIF(c.CUSTOMER_SHORT_NAME, ''), NULLIF(c.CUSTOMER_NAME, ''), m.CUSTOMER_NO) AS CustomerName,
        NULLIF(m.START_TIME, '19000101') AS StartTime, NULLIF(m.EXPECTED_TIME, '19000101') AS ExpectedTime,
        NULLIF(m.END_TIME, '19000101') AS EndTime, NULLIF(LTRIM(RTRIM(m.IS_ORDER)), '') AS IsOrder,
        CAST(CASE WHEN m.ENABLE_FLAG = 'Y' THEN 1 ELSE 0 END AS bit) AS Enabled,
        ISNULL(stats.DetailCount, 0) AS DetailCount, ISNULL(stats.CompletedDetailCount, 0) AS CompletedDetailCount,
        ISNULL(stats.OverdueDetailCount, 0) AS OverdueDetailCount, m.EDIT_TIME AS EditTime
        """;

    private const string ProjectSelectSql = "SELECT " + ProjectColumnsSql + " FROM PRJ_MASTER m " + ProjectJoinsSql + " " + ProjectWhereSql;
    private const string ProjectCountSql = "SELECT COUNT(1) FROM PRJ_MASTER m " + ProjectWhereSql;
    private const string ProjectDetailSelectSql = "SELECT " + ProjectColumnsSql + ", m.CREATE_USER AS CreateUser, m.CREATE_TIME AS CreateTime, m.EDIT_USER AS EditUser FROM PRJ_MASTER m " + ProjectJoinsSql;

    private const string DetailWhereSql = """
        WHERE d.PROJECT_CODE = @ProjectCode
          AND (@Keyword IS NULL OR d.SUMMARY LIKE '%' + @Keyword + '%' OR d.COMMENT LIKE '%' + @Keyword + '%')
          AND (@StatusNo IS NULL OR TRY_CONVERT(decimal(18,0), d.PROJECT_STATUS) = @StatusNo)
          AND (@ProcessTypeNo IS NULL OR TRY_CONVERT(decimal(18,0), d.PROCESS_TYPE) = @ProcessTypeNo)
          AND (@UserAccount IS NULL OR d.PRINCIPAL_USER = @UserAccount OR d.SUPPORT_USER = @UserAccount OR d.REVIEWER_USER = @UserAccount)
          AND (@EnableFlag IS NULL OR d.ENABLE_FLAG = @EnableFlag)
          AND (@StartFrom IS NULL OR NULLIF(d.START_TIME, '19000101') >= @StartFrom)
          AND (@EndTo IS NULL OR NULLIF(d.END_TIME, '19000101') <= @EndTo)
        """;

    private const string DetailCountSql = "SELECT COUNT(1) FROM PRJ_DETAIL d " + DetailWhereSql;
    private const string DetailSelectSql = """
        SELECT d.PRJ_DETAIL_SID AS DetailSid, d.PROJECT_CODE AS ProjectCode,
               TRY_CONVERT(decimal(18,0), d.PROCESS_TYPE) AS ProcessTypeNo, pt.PROCESS_NAME AS ProcessTypeName,
               d.SUMMARY AS Summary, TRY_CONVERT(decimal(18,0), d.PROJECT_STATUS) AS StatusNo,
               ds.PROJECT_STATUS_NAME AS StatusName,
               CAST(CASE WHEN ds.PROJECT_STATUS_FLAG = 'Y' THEN 1 ELSE 0 END AS bit) AS IsCompleted,
               CAST(CASE WHEN ds.PROJECT_STATUS_FLAG <> 'Y' AND NULLIF(d.EXPECTED_TIME, '19000101') < CAST(GETDATE() AS date) THEN 1 ELSE 0 END AS bit) AS IsOverdue,
               d.COMMENT AS Comment, d.PRINCIPAL_USER AS PrincipalUser, pu.USER_NAME AS PrincipalUserName,
               d.SUPPORT_USER AS SupportUser, su.USER_NAME AS SupportUserName,
               d.REVIEWER_USER AS ReviewerUser, ru.USER_NAME AS ReviewerUserName,
               NULLIF(d.START_EXPECTED_TIME, '19000101') AS StartExpectedTime,
               NULLIF(d.START_TIME, '19000101') AS StartTime, NULLIF(d.EXPECTED_TIME, '19000101') AS ExpectedTime,
               NULLIF(d.END_TIME, '19000101') AS EndTime, d.SEQ AS Seq,
               CAST(CASE WHEN d.ENABLE_FLAG = 'Y' THEN 1 ELSE 0 END AS bit) AS Enabled,
               d.FILE_NAME AS FileName, d.CREATE_USER AS CreateUser, d.CREATE_TIME AS CreateTime,
               d.EDIT_USER AS EditUser, d.EDIT_TIME AS EditTime
          FROM PRJ_DETAIL d
          LEFT JOIN PRJ_DETAIL_PROCESS_TYPE pt ON pt.PROCESS_TYPE = TRY_CONVERT(decimal(18,0), d.PROCESS_TYPE)
          LEFT JOIN PRJ_DETAIL_STATUS ds ON ds.PROJECT_STATUS_NO = TRY_CONVERT(decimal(18,0), d.PROJECT_STATUS)
          LEFT JOIN ADM_USER pu ON pu.ACCOUNT_NO = d.PRINCIPAL_USER
          LEFT JOIN ADM_USER su ON su.ACCOUNT_NO = d.SUPPORT_USER
          LEFT JOIN ADM_USER ru ON ru.ACCOUNT_NO = d.REVIEWER_USER
        """;
}
