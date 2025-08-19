using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces.Transaction;

public interface ITransactionService
{
    void WithTransaction(Action<SqlTransaction> action);
    T WithTransaction<T>(Func<SqlTransaction, T> func);
}