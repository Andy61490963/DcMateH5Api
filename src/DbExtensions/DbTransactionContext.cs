using Microsoft.Data.SqlClient;

namespace DbExtensions;

public sealed class DbTransactionContext
{
    public SqlTransaction? Current { get; private set; }

    public bool HasTransaction
    {
        get { return Current != null; }
    }

    public void Set(SqlTransaction tx)
    {
        Current = tx;
    }

    public void Clear()
    {
        Current = null;
    }
}