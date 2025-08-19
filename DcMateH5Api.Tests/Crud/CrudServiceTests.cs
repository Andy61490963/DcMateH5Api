using System.Data;
using Dapper;
using Moq;
using Microsoft.Data.SqlClient;
using Xunit;
using DcMateH5Api.Logging;

public class CrudServiceTests
{
    [Fact]
    public async Task InsertAsync_PassesSqlAndParams()
    {
        var builder = new CrudSqlBuilder();
        var dto = new { Name = "A" };
        var expected = builder.BuildInsert("Users", dto);
        var mockDb = new Mock<IDbExecutor>();
        mockDb.Setup(d => d.ExecuteAsync(expected.Sql, expected.Params, null, CommandType.Text, It.IsAny<CancellationToken>()))
              .ReturnsAsync(1)
              .Verifiable();
        var mockLog = new Mock<ISqlLogService>();
        mockLog.Setup(l => l.LogAsync(It.IsAny<SqlLogEntry>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        var svc = new CrudService(builder, mockDb.Object, mockLog.Object);
        var result = await svc.InsertAsync("Users", dto, CancellationToken.None);
        Assert.Equal(1, result);
        mockDb.Verify();
        mockLog.Verify();
    }

    [Fact]
    public async Task UpdateAsync_WithTransaction_PassesSqlAndParams()
    {
        var builder = new CrudSqlBuilder();
        var mockDb = new Mock<IDbExecutor>();
        var setDto = new { Name = "B" };
        var whereDto = new { Id = 2 };
        var expected = builder.BuildUpdate("Users", setDto, whereDto);
        mockDb.Setup(d => d.ExecuteAsync(It.IsAny<SqlConnection>(), It.IsAny<SqlTransaction?>(), expected.Sql, It.Is<object>(p => p is DynamicParameters), null, CommandType.Text, It.IsAny<CancellationToken>()))
              .ReturnsAsync(1)
              .Verifiable();
        var mockLog = new Mock<ISqlLogService>();
        mockLog.Setup(l => l.LogAsync(It.IsAny<SqlLogEntry>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        var svc = new CrudService(builder, mockDb.Object, mockLog.Object);
        using var conn = new SqlConnection();
        SqlTransaction? tx = null;
        var result = await svc.UpdateAsync(conn, tx, "Users", setDto, whereDto, CancellationToken.None);
        Assert.Equal(1, result);
        mockDb.Verify();
        mockLog.Verify();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsBool()
    {
        var builder = new CrudSqlBuilder();
        var whereDto = new { Id = 3 };
        var expected = builder.BuildExists("Users", whereDto);
        var mockDb = new Mock<IDbExecutor>();
        mockDb.Setup(d => d.ExecuteScalarAsync<int?>(expected.Sql, expected.Params, null, CommandType.Text, It.IsAny<CancellationToken>()))
              .ReturnsAsync(1)
              .Verifiable();
        var mockLog = new Mock<ISqlLogService>();
        mockLog.Setup(l => l.LogAsync(It.IsAny<SqlLogEntry>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        var svc = new CrudService(builder, mockDb.Object, mockLog.Object);
        var exists = await svc.ExistsAsync("Users", whereDto, CancellationToken.None);
        Assert.True(exists);
        mockDb.Verify();
        mockLog.Verify();
    }
}
