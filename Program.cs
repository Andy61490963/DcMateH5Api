using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Interfaces.FormLogic;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using DcMateH5Api.Areas.Form.Options;
using DcMateH5Api.Areas.Form.Services;
using DcMateH5Api.Areas.Form.Services.FormLogic;
using DcMateH5Api.Areas.Form.Services.Transaction;
using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.Areas.Log.Services;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.Services;
using DcMateH5Api.Authorization;
using DcMateH5Api.DbExtensions;
using DcMateH5Api.Helper;
using DcMateH5Api.Services.Cache;
using DcMateH5Api.SqlHelper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 關鍵設定：金鑰持久化 ---
// 1. 指定存放路徑：在專案根目錄下建立「SecurityKeys」資料夾
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "SecurityKeys");

// 2. 確保資料夾存在
if (!Directory.Exists(keysPath))
{
    Directory.CreateDirectory(keysPath);
}

// 3. 配置資料保護
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath)) // 將 XML 金鑰存入硬碟
    .SetApplicationName("DcMateH5Api") // 固定名稱，這對正確解密非常重要
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // 金鑰 90 天後自動輪替

// 註冊 CORS 服務
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", p =>
    {
        p.SetIsOriginAllowed(origin => true) // 動態允許所有來源
         .AllowAnyMethod()
         .AllowAnyHeader()
         .AllowCredentials(); // 允許跨 IP 存取 Cookie
    });
});

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
builder.Services.AddScoped<SQLGenerateHelper>();

// 核心功能註冊
builder.Services.AddScoped<ILogService, LogService>();
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
builder.Services.AddScoped<IFormDeleteGuardService, FormDeleteGuardService>();
builder.Services.AddScoped<IFormMasterDetailService, FormMasterDetailService>();
builder.Services.AddScoped<IFormMultipleMappingService, FormMultipleMappingService>();
builder.Services.AddScoped<IDropdownSqlSyncService, DropdownSqlSyncService>();

// Menu Tree
builder.Services.AddScoped<DCMATEH5API.Areas.Menu.Services.IMenuService, DCMATEH5API.Areas.Menu.Services.MenuService>();

// 工作站與交易
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 授權 Policy
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// -------------------- CORS --------------------
//const string CorsPolicy = "AllowAll";
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy(CorsPolicy, p =>
//        p.AllowAnyOrigin()
//         .AllowAnyMethod()
//         .AllowAnyHeader());
//});

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
        options.Cookie.Path = "/";

        // 保持您的安全設定，相容集中式佈署 (HTTP 同源)
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(expireMinutes);
        options.SlidingExpiration = true;

        options.Events = new CookieAuthenticationEvents
        {
            // 修正重點：Cookie 驗證中，攔截請求的正確事件是 OnValidatePrincipal
            OnValidatePrincipal = context =>
            {
                // 如果 Cookie 沒抓到，我們張開眼睛去看看 Header 有沒有
                if (!context.Principal.Identity.IsAuthenticated)
                {
                    // 這裡的邏輯需要配合自定義的 Middleware 處理更順暢
                }
                return Task.CompletedTask;
            },

            // 針對 API 調整：未授權回傳 401
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
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
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // /因在 ASP.NET Core 3.0 之後，內建的 System.Text.Json 預設會用 camelCase（小駝峰）序列化屬性名稱
    });

// -------------------- 健康檢查 --------------------
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

// -------------------- Swagger --------------------
var swaggerGroups = new[]
{
    SwaggerGroups.ApiStatus, SwaggerGroups.Enum, SwaggerGroups.Log,
    SwaggerGroups.Security, SwaggerGroups.Menu,
    SwaggerGroups.Form, SwaggerGroups.FormWithMasterDetail, SwaggerGroups.FormWithMultipleMapping,
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

    // 1. 加入您的 ManualToken 定義
    options.AddSecurityDefinition("ManualToken", new OpenApiSecurityScheme
    {
        Name = "X-Auth-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "請輸入登入回傳的加密金鑰 (Token)"
    });

    // 2. 關鍵修正：將原本的 AddSecurityRequirement 替換為包含兩者的組合
    options.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ManualToken" }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    //options.AddSecurityRequirement(new OpenApiSecurityRequirement {
    //    {
    //        new OpenApiSecurityScheme {
    //            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    //        },
    //        Array.Empty<string>()
    //    }
    //});

    options.DocInclusionPredicate((doc, api) => doc == "v1" || string.Equals(api.GroupName, doc, StringComparison.OrdinalIgnoreCase));
});

// =====================================================================

var app = builder.Build();

// --- 加入這兩行 ---
app.UseDefaultFiles(); // 讓系統自動找 index.html
app.UseStaticFiles();  // 啟用 wwwroot 靜態檔案支援

// 關鍵順序：必須在 Authentication 之前啟用
app.UseCors("CorsPolicy");
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UseRouting();
//app.UseCors(CorsPolicy);
app.UseMiddleware<GlobalExceptionMiddleware>();

// --- 核心改動：將 Header Token 轉入 Cookie 字串中 ---
app.Use(async (context, next) =>
{
    // 1. 檢查 Header 是否有傳入 Token
    if (context.Request.Headers.TryGetValue("X-Auth-Token", out var token) && !string.IsNullOrEmpty(token))
    {
        // 2. 終極方案：直接在 Request Header 的 "Cookie" 欄位中注入金鑰字串
        // 這就像是在信封袋外面貼上標籤，後續的驗證機制一定能張開眼睛讀取到它
        context.Request.Headers.Append("Cookie", $"DcMateAuthTicket={token}");
    }
    await next();
});

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