using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.Menu;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;
using Microsoft.Data.SqlClient;
using DcMateH5Api.Services.Cache;
using DcMateH5Api.SqlHelper;
using DcMateH5Api.Areas.Permission.Mappers;
using System.Linq;
using DcMateH5Api.Models;

namespace DcMateH5Api.Areas.Permission.Services
{
    /// <summary>
    /// 權限服務的實作類別，負責透過 Dapper 與資料庫交互，管理群組、權限、功能、選單及其關聯設定。
    /// 提供 CRUD 與權限檢查功能，並搭配快取提升效能。
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly SQLGenerateHelper _sqlHelper;
        private readonly IDbExecutor _db;
        private readonly SqlConnection _con;
        private readonly ICacheService _cache; // Redis 快取服務

        /// <summary>
        /// 建構函式，注入資料庫連線與快取服務。
        /// </summary>
        public PermissionService(SQLGenerateHelper sqlHelper, IDbExecutor db, SqlConnection con, ICacheService cache)
        {
            _sqlHelper = sqlHelper;
            _db = db;
            _con = con;
            _cache = cache;
        }
    }
}
