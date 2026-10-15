using Grove.Display.Icons.Iconify;
using Microsoft.AspNetCore.Hosting;

namespace Grove.Services;

public sealed class IconifyLocalMirrorPathProvider(IWebHostEnvironment environment) : IIconifyLocalMirrorPathProvider
{
    public string RootPath => Path.Combine(environment.ContentRootPath, "App_Data", "Grove", "Icons", "IconifyCache");
}
