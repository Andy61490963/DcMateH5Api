using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using DynamicForm.Authorization;
using DynamicForm.Areas.Enum.Interfaces;
using DynamicForm.Areas.Enum.Services;
using DynamicForm.Areas.Form.Interfaces;
using DynamicForm.Areas.Form.Interfaces.FormLogic;
using DynamicForm.Areas.Form.Interfaces.Transaction;
using DynamicForm.Areas.Form.Services;
using DynamicForm.Areas.Form.Services.FormLogic;
using DynamicForm.Areas.Form.Services.Transaction;
using DynamicForm.Areas.Permission.Interfaces;
using DynamicForm.Areas.Permission.Services;
using DynamicForm.Areas.Security.Interfaces;
using DynamicForm.Areas.Security.Models;
using DynamicForm.Areas.Security.Services;
using DynamicForm.DbExtensions;
using DynamicForm.Helper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.WebHost.UseUrls("http://0.0.0.0:5000"); // 目前只開 HTTP

// ---- Swagger Groups ----
var swaggerGroups = new[]
{
    SwaggerGroups.Form,
    SwaggerGroups.Permission,
    SwaggerGroups.Security,
    SwaggerGroups.Enum,
    SwaggerGroups.ApiStats
};

// ---- Swagger ----
// ---- Swagger ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // 加回 v1（包含全部 API，解決 UI 預設抓 /swagger/v1/swagger.json 404）
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API (All)",
        Version = "v1",
        Description = "右上角有分類可以選取"
    });

    // 各分組 doc
    foreach (var group in swaggerGroups)
    {
        options.SwaggerDoc(group, new OpenApiInfo
        {
            Title = $"{SwaggerGroups.DisplayNames[group]} API",
            Version = "v1",
            Description = $"DynamicForm - {group} endpoints"
        });
    }

    // XML 註解（需在 .csproj 打開 <GenerateDocumentationFile>true</GenerateDocumentationFile>）
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // 正確的 Bearer 設定（Type=Http + Scheme=bearer）
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "輸入Token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    // v1 顯示全部；分組只顯示自己的
    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (docName == "v1") return true; // 全部 API
        return string.Equals(apiDesc.GroupName, docName, StringComparison.OrdinalIgnoreCase);
    });
});

// ---- Options / Cache / Config ----
builder.Services.AddOptions();
builder.Services.AddMemoryCache();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// ---- DI 註冊 ----

builder.Services.Configure<DbOptions>( builder.Configuration.GetSection("ConnectionStrings"));

// SqlConnectionFactory 負責建立 SqlConnection，本身無狀態 (stateless)，
// 所以在整個應用程式中共用一個實例，用 Singleton。
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

// DbExecutor 負責執行查詢/交易，需要維持在單一 HTTP Request 的範圍內，
// 保證同一個 Request 共用同一個 Executor，但不會跨 Request 共用 → 使用 Scoped。
builder.Services.AddScoped<IDbExecutor, DbExecutor>();

// 用完立即 Dispose() 歸還池子，其他請求可馬上用，吞吐量高
builder.Services.AddScoped<SqlConnection, SqlConnection>(_ =>
{
    var conn = new SqlConnection();
    conn.ConnectionString = builder.Configuration.GetConnectionString("Connection");
    return conn;
});

// Authorization（避免重複註冊）
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Services
builder.Services.AddScoped<IEnumListService, EnumListService>();
builder.Services.AddScoped<IFormListService, FormListService>();
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

// ---- CORS ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ---- AuthN / AuthZ ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
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
    .ConfigureApiBehaviorOptions(opt =>
    {
        // 讓 400 維持 RFC 7807 ProblemDetails（可在此客製化）
        // opt.InvalidModelStateResponseFactory = ...
    });

var app = builder.Build();

// ---- Pipeline ----
app.UseCors("AllowAll");

// 目前因docker維持 http://0.0.0.0:5000，先不要轉 https，否則會 307 轉到不存在的 https 連線
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./swagger/v1/swagger.json", "All APIs v1");
    foreach (var group in swaggerGroups)
        options.SwaggerEndpoint($"./swagger/{group}/swagger.json", $"{group} API v1");

    options.RoutePrefix = string.Empty;
});

// Area 路由
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.Run();