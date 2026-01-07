using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces.Transaction;

public interface ITransactionService
{
    void WithTransaction(Action<SqlTransaction> action);
    T WithTransaction<T>(Func<SqlTransaction, T> func);
    
    Task WithTransactionAsync(
        Func<SqlTransaction, CancellationToken, Task> action,
        CancellationToken ct = default);

    Task<T> WithTransactionAsync<T>(
        Func<SqlTransaction, CancellationToken, Task<T>> func,
        CancellationToken ct = default);
}