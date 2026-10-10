using Microsoft.AspNetCore.Http;

namespace Crest.DisplayManagement.Razor;

internal sealed class PlatformDisplayHelper : IPlatformDisplayHelper
{
    public PlatformDisplayHelper(HttpContext context, IDisplayHelper displayHelper)
    {
        HttpContext = context;
        DisplayHelper = displayHelper;
    }

    public HttpContext HttpContext { get; set; }
    public IDisplayHelper DisplayHelper { get; set; }
}
