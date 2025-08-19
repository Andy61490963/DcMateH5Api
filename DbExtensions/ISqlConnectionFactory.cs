using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DcMateH5Api.DbExtensions;

public interface ISqlConnectionFactory
{
    SqlConnection Create(); // 只建構，不開啟
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IOptions<DbOptions> options)
    {
        _connectionString = options.Value.Connection 
                            ?? throw new ArgumentNullException(nameof(options.Value.Connection));
    }

    public SqlConnection Create() => new SqlConnection(_connectionString);
}
