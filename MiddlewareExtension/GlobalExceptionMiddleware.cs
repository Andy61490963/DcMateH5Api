using ClassLibrary;
using ClassLibrary.SystemError;
using Microsoft.Data.SqlClient;
using DcMateH5Api.Models;

/// <summary>
/// 捕捉那些非商業邏輯預期的錯誤
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SqlException ex)
        {
            await WriteError(context,
                SystemErrorCode.DatabaseError,
                ex,
                StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            await WriteError(context,
                SystemErrorCode.UnhandledException,
                ex,
                StatusCodes.Status500InternalServerError);
        }
    }

    private async Task WriteError(
        HttpContext context,
        Enum code,
        Exception ex,
        int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorData = ExceptionDumpHelper.BuildExceptionDump(ex);

        var result = Result<object>.Fail(
            code,
            code.GetDescription(),
            errorData
        );

        await context.Response.WriteAsJsonAsync(result);
    }
}