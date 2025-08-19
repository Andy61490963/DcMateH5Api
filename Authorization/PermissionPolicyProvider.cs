using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DynamicForm.Authorization
{
    /// <summary>
    /// 動態產生基於權限的政策，讓 [RequirePermission] 可使用任意權限字串。
    /// </summary>
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
            => _fallback = new DefaultAuthorizationPolicyProvider(options);

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(RequireControllerPermissionAttribute.PolicyPrefix))
                return _fallback.GetPolicyAsync(policyName);

            var actionStr = policyName.Substring(RequireControllerPermissionAttribute.PolicyPrefix.Length);
            if (!int.TryParse(actionStr, out var actionCode))
                return Task.FromResult<AuthorizationPolicy?>(null); 

            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirementScopedToController(actionCode))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
    }
}
