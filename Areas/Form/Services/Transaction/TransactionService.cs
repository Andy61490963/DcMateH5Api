using System.Data;
using DcMateH5Api.Areas.Form.Interfaces.Transaction;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Services.Transaction;

/// <summary>
/// 實作資料庫交易邏輯，封裝 Commit / Rollback 控制
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly SqlConnection _con;

    public TransactionService(SqlConnection connection)
    {
        _con = connection;
    }

    // ============================================================
    // Connection helpers
    // ============================================================

    private void EnsureConnectionOpen()
    {
        if (_con.State != ConnectionState.Open)
        {
            _con.Open();
        }
    }

    private async Task EnsureConnectionOpenAsync(CancellationToken ct)
    {
        if (_con.State != ConnectionState.Open)
        {
            await _con.OpenAsync(ct);
        }
    }

    // ============================================================
    // Existing sync APIs（完全保留）
    // ============================================================

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

    // ============================================================
    // Existing async APIs（保留，但僅適合「不需要 connection」的情境）
    // ============================================================

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
            try { await tx.RollbackAsync(ct); } catch { }
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

    // ============================================================
    // 新增：正確的 async API（conn + tx 一起給）
    // ============================================================

    public async Task WithTransactionAsync(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        await EnsureConnectionOpenAsync(ct);

        await using var tx = (SqlTransaction)await _con.BeginTransactionAsync(ct);
        try
        {
            await action(_con, tx, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    public async Task<T> WithTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> func,
        CancellationToken ct = default)
    {
        await EnsureConnectionOpenAsync(ct);

        await using var tx = (SqlTransaction)await _con.BeginTransactionAsync(ct);
        try
        {
            var result = await func(_con, tx, ct);
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
