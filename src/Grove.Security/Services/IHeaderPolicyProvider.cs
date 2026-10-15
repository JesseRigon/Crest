using Microsoft.AspNetCore.Http;

namespace Grove.Security.Services;

public interface IHeaderPolicyProvider
{
    void InitializePolicy();

    void ApplyPolicy(HttpContext httpContext);
}
