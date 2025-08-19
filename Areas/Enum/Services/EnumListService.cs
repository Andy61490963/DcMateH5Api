using DynamicForm.Areas.Enum.Interfaces;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Enum.Services;

public class EnumListService : IEnumListService
{
    private readonly SqlConnection _con;
    
    public EnumListService(SqlConnection connection)
    {
        _con = connection;
    }
}