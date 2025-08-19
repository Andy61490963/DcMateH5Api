using System;
using ClassLibrary;
using Microsoft.AspNetCore.Authorization;

namespace DynamicForm.Authorization
{
    /// <summary>
    /// 指定存取此端點所需的權限代碼。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequireControllerPermissionAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "PERM:";

        public RequireControllerPermissionAttribute(ActionType action)
        {
            // 只先放動作，Area/Controller 在 PolicyProvider 取 RouteData 再組合
            Policy = $"{PolicyPrefix}{(int)action}";
        }
    }
}
