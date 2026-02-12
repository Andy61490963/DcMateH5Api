using DbExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DcMateH5.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDcMateH5Infrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1) DbOptions：由 Host 的 IConfiguration 綁定（不管是 DLL 或 ProjectReference 都一樣）
        services
            .AddOptions<DbOptions>()
            .Bind(configuration.GetSection(DbOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Connection),
                $"{DbOptions.SectionName}:{nameof(DbOptions.Connection)} 不可為空")
            .ValidateOnStart();

        // 2) 連線工廠（Singleton 安全，因為只拿 options，不共用 SqlConnection 實例）
        services.TryAddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

        // 2.5) SqlConnection（Scoped：每個 Request 一條；由 DI Scope 結束時 Dispose）
        services.TryAddScoped<SqlConnection>(sp =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DbOptions>>().Value;

            // 這裡不 Open()，避免無效佔用連線；真正用到時才開（SqlClient 有 pooling）
            return new SqlConnection(dbOptions.Connection);
        });

        return services;
    }
}