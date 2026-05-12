using DbExtensions;
using DbExtensions.DbExecutor.Interface;
using DbExtensions.DbExecutor.Service;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Eqm;
using DcMateH5.Abstractions.Export;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.FormLogic;
using DcMateH5.Abstractions.Form.Options;
using DcMateH5.Abstractions.Form.Transaction;
using DcMateH5.Abstractions.LanguageKeywords;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Menu;
using DcMateH5.Abstractions.Mms;
using DcMateH5.Abstractions.RegistrationLicense;
using DcMateH5.Abstractions.Token;
using DcMateH5.Abstractions.Token.Model;
using DcMateH5.Abstractions.Wip;
using DcMateH5.Infrastructure.Eqm;
using DcMateH5.Infrastructure.Export;
using DcMateH5.Infrastructure.Form.Form;
using DcMateH5.Infrastructure.Form.FormLogic;
using DcMateH5.Infrastructure.Form.Transaction;
using DcMateH5.Infrastructure.LanguageKeywords;
using DcMateH5.Infrastructure.Log;
using DcMateH5.Infrastructure.Menu;
using DcMateH5.Infrastructure.Mms;
using DcMateH5.Infrastructure.RegistrationLicense;
using DcMateH5.Infrastructure.Token;
using DcMateH5.Infrastructure.Wip;
using DcMateH5Api.Areas.Security.Options;
using DcMateH5Api.BackgroundService;
using DcMateH5Api.MiddlewareExtension;
using DcMateH5Api.MiddlewareExtension.Token;
using DcMateH5Api.Services.Cache;
using DcMateH5Api.Services.Cache.Redis.Interfaces;
using DcMateH5Api.Services.Cache.Redis.Services;
using DcMateH5Api.Services.CurrentUser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using PdfSharp.Fonts;
using System.Reflection;
using AccountService = DcMateH5Api.Areas.Security.Services.AccountService;
using AuthenticationService = DcMateH5Api.Areas.Security.Services.AuthenticationService;
using IAccountService = DcMateH5Api.Areas.Security.Interfaces.IAccountService;
using IAuthenticationService = DcMateH5Api.Areas.Security.Interfaces.IAuthenticationService;
using IEmailSender = DcMateH5Api.Areas.Security.Interfaces.IEmailSender;
using SmtpEmailSender = DcMateH5Api.Areas.Security.Services.SmtpEmailSender;

var builder = WebApplication.CreateBuilder(args);

// 註冊 CORS 服務
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Authorization", "X-Token-Expire");
    });
});

// 容器內對外開 5000
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// -------------------- Config 讀取 --------------------
var config = builder.Configuration;
var redisConn = config.GetValue<string>("Redis:Connection");

builder.Services.Configure<CacheOptions>(config.GetSection("Cache"));
builder.Services.Configure<DbOptions>(config.GetSection("ConnectionStrings"));
builder.Services.Configure<FormSettings>(config.GetSection("FormSettings"));
builder.Services.Configure<TokenOptions>(config.GetSection("TokenOptions"));
builder.Services.Configure<EmailSettingOptions>(config.GetSection(EmailSettingOptions.SectionName));
builder.Services.Configure<PasswordResetOptions>(config.GetSection(PasswordResetOptions.SectionName));

// -------------------- 分散式快取（Redis） --------------------
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = redisConn;
    opt.InstanceName = "DcMateH5Api:";
});

// -------------------- 連線字串 --------------------
builder.Services.AddScoped<SqlConnection>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Connection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:Connection 不可為空");
    }

    return new SqlConnection(connectionString);
});

// -------------------- 基礎服務 --------------------
builder.Services.AddHttpContextAccessor(); // 僅保留這一次註冊
builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();

// Db 工具
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<DbTransactionContext>();
builder.Services.AddScoped<IDbExecutor, DbExecutor>();
builder.Services.AddScoped<SQLGenerateHelper>();

// 核心功能註冊
builder.Services.AddScoped<ILanguageKeywordService, LanguageKeywordService>();
builder.Services.AddHostedService<FormOrphanCleanupHostedService>();
builder.Services.AddScoped<IFormOrphanCleanupService, FormOrphanCleanupService>();
builder.Services.AddScoped<IFormDesignerService, FormDesignerService>();
builder.Services.AddScoped<IFormViewDesignerService, FormViewDesignerService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IRegistrationLicenseService, RegistrationLicenseService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IFormFieldMasterService, FormFieldMasterService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IFormFieldConfigService, FormFieldConfigService>();
builder.Services.AddScoped<IDropdownService, DropdownService>();
builder.Services.AddScoped<IFormDataService, FormDataService>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IFormDeleteGuardService, FormDeleteGuardService>();
builder.Services.AddScoped<IFormMasterDetailService, FormMasterDetailService>();
builder.Services.AddScoped<IFormMultipleMappingService, FormMultipleMappingService>();
builder.Services.AddScoped<IFormDesignerTableValueFunctionService, FormDesignerTableValueFunctionService>();
builder.Services.AddScoped<IDropdownSqlSyncService, DropdownSqlSyncService>();
builder.Services.AddScoped<IFormTableValueFunctionService, FormTableValueFunctionService>();
builder.Services.AddScoped<IFormViewService, FormViewService>();
builder.Services.AddScoped<ILogService, LogService>();

// Menu Tree
builder.Services.AddScoped<IMenuService, MenuService>();

// Wip
builder.Services.AddScoped<ISelectDtoService, SelectDtoService>();
builder.Services.AddScoped<IBaseInfoCheckExistService, BaseInfoCheckExistService>();
builder.Services.AddScoped<IWipBaseSettingService, WipBaseSettingService>();

// Eqm
builder.Services.AddScoped<IEqmStatusService, EqmStatusService>();
builder.Services.AddScoped<ILotBaseSettingService, LotBaseSettingService>();

// Mms
builder.Services.AddScoped<IMmsLotService, MmsLotService>();

// 工作站與交易
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Export Pdf匯出(Excel待補)
builder.Services.AddScoped<DcMateH5.Abstractions.Export.Pdf.IPdfExportService, DcMateH5.Infrastructure.Export.Pdf.PdfExportService>();

// 宣告 產生pdf使用字體
GlobalFontSettings.FontResolver = new MyFontResolver();

// 註冊 Authentication Scheme
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CustomTokenAuthenticationHandler.CustomTokenAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CustomTokenAuthenticationHandler.CustomTokenAuthenticationDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = CustomTokenAuthenticationHandler.CustomTokenAuthenticationDefaults.AuthenticationScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, CustomTokenAuthenticationHandler>(
        CustomTokenAuthenticationHandler.CustomTokenAuthenticationDefaults.AuthenticationScheme,
        options => { });

builder.Services.AddAuthorization();

builder.Services.AddControllers(options =>
    {
        // options.Filters.Add<DcMateH5Api.Areas.Security.Filters.ConcurrentLoginCheckFilter>(); 
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // /因在 ASP.NET Core 3.0 之後，內建的 System.Text.Json 預設會用 camelCase（小駝峰）序列化屬性名稱
    });

// -------------------- 健康檢查 --------------------
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

// -------------------- Swagger --------------------
var swaggerGroups = new[]
{
    SwaggerGroups.ApiStatus, SwaggerGroups.Enum, SwaggerGroups.Log,
    SwaggerGroups.Security, SwaggerGroups.Menu, SwaggerGroups.LanguageKeywords,
    SwaggerGroups.Form, SwaggerGroups.FormWithMasterDetail, SwaggerGroups.FormWithMultipleMapping,
    SwaggerGroups.FormTableValueFunction, SwaggerGroups.FormView, SwaggerGroups.Wip,
    SwaggerGroups.Eqm, SwaggerGroups.Mms
};

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "API (All)", Version = "v1" });
    foreach (var g in swaggerGroups)
    {
        var displayName = SwaggerGroups.DisplayNames.TryGetValue(g, out var name) ? name : g;
        options.SwaggerDoc(g, new OpenApiInfo { Title = $"{displayName} API", Version = "v1" });
    }

    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
    
    // 改成標準 Bearer Token 設定
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "請輸入 Token。格式：Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Bearer"
    });

    // 套用 Bearer Security Requirement
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    options.DocInclusionPredicate((doc, api) => doc == "v1" || string.Equals(api.GroupName, doc, StringComparison.OrdinalIgnoreCase));
});

// =====================================================================

var app = builder.Build();

// --- 加入這兩行 ---
app.UseDefaultFiles(); // 讓系統自動找 index.html
app.UseStaticFiles();  // 啟用 wwwroot 靜態檔案支援

// 關鍵順序：必須在 Authentication 之前啟用
app.UseCors("AllowAll");
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UseRouting();
//app.UseCors(CorsPolicy);

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();

// 認證完後，授權前插入續期 middleware
app.UseTokenRenew();

app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("swagger/v1/swagger.json", "All APIs v1");
    foreach (var g in swaggerGroups)
        options.SwaggerEndpoint($"swagger/{g}/swagger.json", $"{g} API v1");
    options.RoutePrefix = string.Empty;
});

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
