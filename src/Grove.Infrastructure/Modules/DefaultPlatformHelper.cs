using Microsoft.AspNetCore.Http;

namespace Grove.Modules;

public class DefaultPlatformHelper : IPlatformHelper
{
    public DefaultPlatformHelper(IHttpContextAccessor httpContextAccessor)
    {
        HttpContext = httpContextAccessor.HttpContext;
    }

    public HttpContext HttpContext { get; set; }
}
