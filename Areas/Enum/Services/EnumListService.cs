using DcMateH5Api.Areas.Enum.Interfaces;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Enum.Services;

public class EnumListService : IEnumListService
{
    private readonly SqlConnection _con;
    
    public EnumListService(SqlConnection connection)
    {
        _con = connection;
    }
}