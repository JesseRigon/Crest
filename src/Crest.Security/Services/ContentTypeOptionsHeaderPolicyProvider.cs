using Microsoft.AspNetCore.Http;

namespace Crest.Security.Services;

public class ContentTypeOptionsHeaderPolicyProvider : HeaderPolicyProvider
{
    public override void ApplyPolicy(HttpContext httpContext)
    {
        httpContext.Response.Headers[SecurityHeaderNames.XContentTypeOptions] = Options.ContentTypeOptions;
    }
}
