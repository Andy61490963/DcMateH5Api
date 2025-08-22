using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using DcMateH5Api.Authorization;
using DcMateH5Api.Areas.Enum.Interfaces;
using DcMateH5Api.Areas.Enum.Services;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Services;
using DcMateH5Api.Areas.Form.Services.FormLogic;
using DcMateH5Api.Areas.Form.Services.Transaction;
using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.Areas.Log.Services;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Services;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.Services;
using DcMateH5Api.DbExtensions;
using DcMateH5Api.Helper;
using DcMateH5Api.Services.Cache;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// 容器內對外開 5000（你有 Nginx 在前面做 8081 反代）
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// -------------------- Config 讀取（有 fallback） --------------------
var config = builder.Configuration;

var redisConn = builder.Configuration.GetValue<string>("Redis:Connection");

var jwt = config.GetSection("JwtSettings").Get<JwtSettings>()
          ?? throw new InvalidOperationException("JwtSettings missing.");

builder.Services.Configure<JwtSettings>(config.GetSection("JwtSettings"));
builder.Services.Configure<CacheOptions>(config.GetSection("Cache"));
builder.Services.Configure<DbOptions>(config.GetSection("ConnectionStrings"));

// -------------------- 分散式快取（Redis） --------------------
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration  = redisConn;
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddOptions();

// Db 工具（你既有的抽象）
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IDbExecutor, DbExecutor>();

// 業務服務
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IEnumListService, EnumListService>();
builder.Services.AddScoped<IFormDesignerService, FormDesignerService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ITokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IFormFieldMasterService, FormFieldMasterService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IFormFieldConfigService, FormFieldConfigService>();
builder.Services.AddScoped<IDropdownService, DropdownService>();
builder.Services.AddScoped<IFormDataService, FormDataService>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// 快取服務（你自訂的包裝）
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 授權（自訂 Policy/Handler）
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// -------------------- CORS（開發寬鬆；正式請收斂網域） --------------------
const string CorsPolicy = "AllowAll";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

// -------------------- AuthN / AuthZ（JWT） --------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // 因在 ASP.NET Core 3.0 之後，內建的 System.Text.Json 預設會用 camelCase（小駝峰）序列化屬性名稱
    });

// -------------------- 健康檢查（給 LB/Nginx） --------------------
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

// -------------------- Swagger（分組 + Bearer） --------------------
var swaggerGroups = new[]
{
    SwaggerGroups.Form,
    SwaggerGroups.Permission,
    SwaggerGroups.Security,
    SwaggerGroups.Enum,
    SwaggerGroups.ApiStats,
    SwaggerGroups.Log
};

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API (All)",
        Version = "v1",
        Description = "右上角可切換分組"
    });

    foreach (var g in swaggerGroups)
    {
        options.SwaggerDoc(g, new OpenApiInfo
        {
            Title = $"{SwaggerGroups.DisplayNames[g]} API",
            Version = "v1",
            Description = $"DcMateH5Api - {g} endpoints"
        });
    }

    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "在此輸入 JWT：Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    options.DocInclusionPredicate((doc, api) =>
        doc == "v1" || string.Equals(api.GroupName, doc, StringComparison.OrdinalIgnoreCase));
});

// =====================================================================

var app = builder.Build();

// Pipeline
app.UseCors(CorsPolicy);

// 不在這裡強制 https（你有 Nginx 統一處理 TLS）
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Dev/Container 下開 Swagger（若 Prod 想關可用環境變數切）
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./swagger/v1/swagger.json", "All APIs v1");
    foreach (var g in swaggerGroups)
        options.SwaggerEndpoint($"./swagger/{g}/swagger.json", $"{g} API v1");
    options.RoutePrefix = string.Empty; // 讓 / 直接是 Swagger UI
});

// Area + Controllers
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

// 健康檢查端點
app.MapHealthChecks("/healthz");

app.Run();
