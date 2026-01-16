using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Options;
using DcMateH5Api.Areas.Form.Services;
using DcMateH5Api.Areas.Form.Services.FormLogic;
using DcMateH5Api.Areas.Form.Services.Transaction;
using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.Areas.Log.Services;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Services;
using DcMateH5Api.Areas.RouteOperation.Interfaces;
using DcMateH5Api.Areas.RouteOperation.Services;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.Services;
using DcMateH5Api.Authorization;
using DcMateH5Api.DbExtensions;
using DcMateH5Api.Helper;
using DcMateH5Api.Services.Cache;
using DcMateH5Api.SqlHelper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using DcMateClassLibrary.Helper;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 容器內對外開 5000
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// -------------------- Config 讀取 --------------------
var config = builder.Configuration;
var redisConn = config.GetValue<string>("Redis:Connection");
var jwt = config.GetSection("JwtSettings").Get<JwtSettings>()
          ?? throw new InvalidOperationException("JwtSettings missing.");

builder.Services.Configure<JwtSettings>(config.GetSection("JwtSettings"));
builder.Services.Configure<CacheOptions>(config.GetSection("Cache"));
builder.Services.Configure<DbOptions>(config.GetSection("ConnectionStrings"));
builder.Services.Configure<FormSettings>(config.GetSection("FormSettings"));

// -------------------- 分散式快取（Redis） --------------------
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = redisConn;
    opt.InstanceName = "DcMateH5Api:";
});

// -------------------- 連線字串 --------------------
builder.Services.AddScoped<SqlConnection, SqlConnection>(_ =>
{
    var conn = new SqlConnection();
    conn.ConnectionString = builder.Configuration.GetConnectionString("Connection");
    return conn;
});

// -------------------- 基礎服務 --------------------
builder.Services.AddHttpContextAccessor(); // 僅保留這一次註冊

// Db 工具
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IDbExecutor, DbExecutor>();
builder.Services.AddScoped<DcMateH5Api.SqlHelper.SQLGenerateHelper>();

// 核心功能註冊
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IFormDesignerService, FormDesignerService>();
builder.Services.AddScoped<DcMateH5Api.Areas.Security.Interfaces.IAuthenticationService, DcMateH5Api.Areas.Security.Services.AuthenticationService>();
builder.Services.AddScoped<ITokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<DcMateH5Api.Areas.Security.Interfaces.IPasswordHasher, DcMateH5Api.Helper.PasswordHasher>();
builder.Services.AddScoped<IFormFieldMasterService, FormFieldMasterService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IFormFieldConfigService, FormFieldConfigService>();
builder.Services.AddScoped<IDropdownService, DropdownService>();
builder.Services.AddScoped<IFormDataService, FormDataService>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IFormDeleteGuardService, FormDeleteGuardService>();
builder.Services.AddScoped<IFormMasterDetailService, FormMasterDetailService>();
builder.Services.AddScoped<IFormMultipleMappingService, FormMultipleMappingService>();
builder.Services.AddScoped<IDropdownSqlSyncService, DropdownSqlSyncService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Menu Tree
builder.Services.AddScoped<DCMATEH5API.Areas.Menu.Services.IMenuService, DCMATEH5API.Areas.Menu.Services.MenuService>();

// 工作站與交易
builder.Services.AddScoped<IBasRouteService, BasRouteService>();
builder.Services.AddScoped<IBasOperationService, BasOperationService>();
builder.Services.AddScoped<IBasConditionService, BasConditionService>();
builder.Services.AddScoped<IRouteOperationService, RouteOperationService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 授權 Policy
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// -------------------- CORS --------------------
const string CorsPolicy = "AllowAll";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

// -------------------- 重要：混合驗證模式 (JWT + Cookie) --------------------
// 修改點：將 Cookie 設為預設 Scheme
var authSettings = builder.Configuration.GetSection("AuthSettings");
var expireMinutes = authSettings.GetValue<int>("ExpireTimeSpanMinutes");
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "DcMateAuthTicket";

        // --- 關鍵修正：將 Cookie 作用域擴大到整個 IP ---
        options.Cookie.Path = "/";
        // 測試用：5 分鐘有效期（只要過 2.5 分鐘有操作就會更新）
        //options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

        // 正式環境建議：1 小時
        // options.ExpireTimeSpan = TimeSpan.FromHours(1); 

        options.ExpireTimeSpan = TimeSpan.FromMinutes(expireMinutes); // 從設定檔讀取

        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context => {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// -------------------- 健康檢查 --------------------
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

// -------------------- Swagger --------------------
var swaggerGroups = new[]
{
    SwaggerGroups.Form, SwaggerGroups.FormWithMasterDetail, SwaggerGroups.FormWithMultipleMapping,
    SwaggerGroups.Menu, SwaggerGroups.Permission, SwaggerGroups.Security,
    SwaggerGroups.Enum, SwaggerGroups.ApiStatus, SwaggerGroups.Log, SwaggerGroups.RouteOperation
};

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "API (All)", Version = "v1" });
    foreach (var g in swaggerGroups)
    {
        options.SwaggerDoc(g, new OpenApiInfo { Title = $"{SwaggerGroups.DisplayNames[g]} API", Version = "v1" });
    }

    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "在此輸入 JWT：Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    options.DocInclusionPredicate((doc, api) => doc == "v1" || string.Equals(api.GroupName, doc, StringComparison.OrdinalIgnoreCase));
});

// =====================================================================

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UseRouting();
app.UseCors(CorsPolicy);
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
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