using Microsoft.AspNetCore.Http;

namespace Crest.Security.Services;

public interface IHeaderPolicyProvider
{
    void InitializePolicy();

    void ApplyPolicy(HttpContext httpContext);
}
