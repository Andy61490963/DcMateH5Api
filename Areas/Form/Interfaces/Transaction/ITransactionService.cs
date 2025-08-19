using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Interfaces.Transaction;

public interface ITransactionService
{
    void WithTransaction(Action<SqlTransaction> action);
    T WithTransaction<T>(Func<SqlTransaction, T> func);
}