using Grove.Extensions;
using Grove.Infrastructure.Shell;
using Microsoft.AspNetCore.Http;

namespace Grove.Display;

/// <summary>
/// The server side of <see cref="IShellRequestPath"/>: the path the shell middleware stashed
/// before it rewrote the request to the page's literal (<see cref="BlazorHosting.OriginalPathItem"/>),
/// made shell-relative by stripping the shell base it also stashed.
/// </summary>
public sealed class ShellRequestPath(IHttpContextAccessor httpContextAccessor) : IShellRequestPath
{
    public string? Query => httpContextAccessor.HttpContext?.Request.QueryString.Value;

    public string? Path
    {
        get
        {
            var items = httpContextAccessor.HttpContext?.Items;
            if (items?.TryGetValue(BlazorHosting.OriginalPathItem, out var original) != true || original is not string path)
            {
                return null;
            }

            if (items.TryGetValue(BlazorHosting.ShellBasePathItem, out var shellBase) && shellBase is string { Length: > 0 } basePath
                && new PathString(path).StartsWithSegments(new PathString(basePath), StringComparison.OrdinalIgnoreCase, out var remainder))
            {
                path = remainder.Value ?? string.Empty;
            }

            return path.Length == 0 ? "/" : path;
        }
    }
}
