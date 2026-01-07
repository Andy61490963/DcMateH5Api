using System.Data;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services.Transaction;

/// <summary>
/// 實作資料庫交易邏輯，封裝 Commit / Rollback 控制
/// 這邊先不要拿掉
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly SqlConnection _con;

    public TransactionService(SqlConnection connection)
    {
        _con = connection;
    }

    /// <summary>
    /// 確保連線已開啟（同步）
/// </summary>
    private void EnsureConnectionOpen()
    {
        if (_con.State != ConnectionState.Open)
            _con.Open();
    }

    /// <summary>
    /// 確保連線已開啟（非同步）
/// </summary>
    private async Task EnsureConnectionOpenAsync(CancellationToken ct)
    {
        if (_con.State != ConnectionState.Open)
            await _con.OpenAsync(ct);
    }

    public void WithTransaction(Action<SqlTransaction> action)
    {
        EnsureConnectionOpen();

        using var tx = _con.BeginTransaction();
        try
        {
            action(tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public T WithTransaction<T>(Func<SqlTransaction, T> func)
    {
        EnsureConnectionOpen();

        using var tx = _con.BeginTransaction();
        try
        {
            var result = func(tx);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task WithTransactionAsync(
        Func<SqlTransaction, CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        await EnsureConnectionOpenAsync(ct);

        await using var tx = (SqlTransaction)await _con.BeginTransactionAsync(ct);
        try
        {
            await action(tx, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { /* rollback 也可能失敗，別蓋掉原始例外 */ }
            throw;
        }
    }

    public async Task<T> WithTransactionAsync<T>(
        Func<SqlTransaction, CancellationToken, Task<T>> func,
        CancellationToken ct = default)
    {
        await EnsureConnectionOpenAsync(ct);

        await using var tx = (SqlTransaction)await _con.BeginTransactionAsync(ct);
        try
        {
            var result = await func(tx, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
