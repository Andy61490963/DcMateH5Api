using System.Data;
using DynamicForm.Areas.Form.Interfaces.Transaction;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Services.Transaction;

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

    /// <summary>
    /// 確保連線已開啟
    /// </summary>
    private void EnsureConnectionOpen()
    {
        if (_con.State != ConnectionState.Open)
            _con.Open();
    }

    /// <summary>
    /// 執行無回傳值的交易操作
    /// </summary>
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

    /// <summary>
    /// 執行有回傳值的交易操作
    /// </summary>
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
}