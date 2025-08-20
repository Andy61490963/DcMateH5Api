//
// using DcMateH5Api.Areas.Security.Interfaces;
//
// namespace DcMateH5Api.Areas.Log.Services
// {
//     public class LogService : ILogService
//     {
//         private readonly IDbExecutor _db;
//         
//         public LogService( IDbExecutor db)
//         {
//             _db = db;
//         }
//
//         private static class Sql
//         {
//             public const string GetUser = @"/**/SELECT ID, NAME AS Account, SWD AS PasswordHash, SWD_SALT AS PasswordSalt FROM SYS_USER WHERE NAME = @Account AND IS_DELETE = 0";
//             public const string CheckSql = @"/**/SELECT COUNT(1) FROM SYS_USER WHERE NAME = @Account AND IS_DELETE = 0";
//             public const string InsertSql = @"/**/
//         INSERT INTO SYS_USER (ID, AC, NAME, SWD, SWD_SALT, ROLE, IS_DELETE)
//         VALUES (@Id, @AC, @Name, @Hash, @Salt, @Role, 0)";
//         }
//     }
// }
