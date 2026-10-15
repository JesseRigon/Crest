#nullable enable
using Grove.Access;
using Grove.Environment.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Media.Library;

/// <summary>
/// Who file-object calls run as when the code that makes them has no caller of its own. A
/// request always has one (the access gate set it); server-side work outside a gated execution
/// (a recipe, a migration, a deferred task) does not.
/// </summary>
public static class MediaCallers
{
    /// <summary>
    /// Runs <paramref name="work"/> as the current caller, or as the tenant's system actor when
    /// there is none. For server-side writes only (the library's file store, the import): never
    /// for anything that renders a URL or a byte for a visitor.
    /// </summary>
    public static async Task<T> AsCallerOrSystemAsync<T>(IServiceProvider services, Func<Task<T>> work)
    {
        var accessor = services.GetService<ICallerContextAccessor>();
        if (accessor is null || accessor.Current is not null)
        {
            return await work();
        }

        var factory = services.GetService<ICallerContextFactory>();
        var runner = services.GetService<IAccessRunner>();
        if (factory is null || runner is null)
        {
            return await work();
        }

        return await runner.RunAsAsync(await factory.CreateSystemAsync(), work);
    }

    public static Task AsCallerOrSystemAsync(IServiceProvider services, Func<Task> work) =>
        AsCallerOrSystemAsync(services, async () =>
        {
            await work();
            return true;
        });

    /// <summary>Runs <paramref name="work"/> as the system actor, whoever the caller is.</summary>
    public static async Task<T> AsSystemAsync<T>(IServiceProvider services, Func<Task<T>> work)
    {
        var factory = services.GetRequiredService<ICallerContextFactory>();
        var runner = services.GetRequiredService<IAccessRunner>();
        return await runner.RunAsAsync(await factory.CreateSystemAsync(), work);
    }

    /// <summary>
    /// Runs <paramref name="work"/> as the current caller, or as an anonymous site visitor when
    /// there is none: what a page rendered outside a request may show is what anyone may see.
    /// </summary>
    public static async Task<T> AsCallerOrAnonymousAsync<T>(IServiceProvider services, Func<Task<T>> work)
    {
        var accessor = services.GetService<ICallerContextAccessor>();
        var runner = services.GetService<IAccessRunner>();
        if (accessor is null || accessor.Current is not null || runner is null)
        {
            return await work();
        }

        var tenant = services.GetService<ShellSettings>()?.Name ?? string.Empty;
        return await runner.RunAsAsync(CallerContext.Anonymous(tenant, CallerSide.Site), work);
    }
}
