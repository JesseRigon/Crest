using Grove.Environment.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Grove.Extensions;

/// <summary>
/// Defaults every host built on Grove wants, so a host's Program.cs does not carry its own
/// copies: one data root for the whole instance, and static files the browser revalidates.
/// </summary>
public static class ServerHostDefaults
{
    /// <summary>
    /// Configuration key naming the instance's data root: the tenants folder, <c>tenants.json</c>,
    /// tenant databases and the global store all live under it. Relative paths resolve against
    /// the content root. Unset keeps the platform default (<c>App_Data</c>). A throwaway test
    /// server passes its own (<c>--Grove:DataRoot=dev/test-instance</c>) so it never touches
    /// the dev server's data.
    /// </summary>
    public const string DataRootKey = "Grove:DataRoot";

    /// <summary>Registers the host defaults; call on the host's services, beside <c>AddPlatformCms()</c>.</summary>
    public static IServiceCollection AddHostDefaults(this IServiceCollection services)
    {
        services.AddTransient<IPostConfigureOptions<ShellOptions>, DataRootShellOptions>();
        return services;
    }

    /// <summary>
    /// Serves static files with <c>Cache-Control: no-cache</c>: the browser keeps them but asks
    /// before each use, and the ETag answers 304 when nothing changed. Without it a rebuilt
    /// module's unfingerprinted assets (scripts, styles, module client libraries) stay stale in
    /// the browser until a hard reload. Use in place of <c>UseStaticFiles()</c>.
    /// </summary>
    public static IApplicationBuilder UseHostStaticFiles(this IApplicationBuilder app) =>
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        });

    /// <summary>The data-root override, applied after the platform's own shell options setup.</summary>
    public static string? ResolveDataRoot(IConfiguration configuration, string contentRootPath)
    {
        var root = configuration[DataRootKey];
        return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root, contentRootPath);
    }

    private sealed class DataRootShellOptions(IConfiguration configuration, IHostEnvironment environment) : IPostConfigureOptions<ShellOptions>
    {
        public void PostConfigure(string? name, ShellOptions options)
        {
            if (ResolveDataRoot(configuration, environment.ContentRootPath) is { } root)
            {
                options.ShellsApplicationDataPath = root;
            }
        }
    }
}
