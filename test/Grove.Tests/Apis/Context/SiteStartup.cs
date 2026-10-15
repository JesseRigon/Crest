using Grove.Data.YesSql;
using Grove.Modules;
using Grove.Modules.Manifest;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Tests.Apis.Context;

public class SiteStartup
{
    public static readonly ConcurrentDictionary<string, PermissionsContext> PermissionsContexts;

    static SiteStartup()
    {
        PermissionsContexts = new ConcurrentDictionary<string, PermissionsContext>();
    }

#pragma warning disable CA1822 // Mark members as static
    public void ConfigureServices(IServiceCollection services)
#pragma warning restore CA1822 // Mark members as static
    {
        services.AddPlatformCms(builder =>
            builder.AddSetupFeatures(
                "Grove.Infrastructure.Tenants"
            )
            .AddTenantFeatures(
                "Grove.Data.Localization",
                "Grove.Data.Protocols.GraphQL"
            )
            .ConfigureServices(collection =>
            {
                collection.Configure<YesSqlOptions>(options =>
                {
                    // To ensure we don't encounter any concurrent issue, enable EnableThreadSafetyChecks for all test.
                    options.EnableThreadSafetyChecks = true;
                });

                collection.AddScoped<IRecipeHarvester, TestRecipeHarvester>();

                collection.AddScoped<IAuthorizationHandler, PermissionContextAuthorizationHandler>(sp =>
                {
                    return new PermissionContextAuthorizationHandler(sp.GetRequiredService<IHttpContextAccessor>(), PermissionsContexts);
                });
            })
            // The declared API route groups (Grove.Access.Users' sign-in, registration, password and
            // two-factor APIs, Grove.Access' antiforgery token, ...): Grove.Infrastructure.Server maps them in a
            // real host and is not part of this one, so the harness maps them the same way, one
            // group per contributor, last registration of a name winning. The antiforgery filter
            // is left out: the tests call the APIs without a browser.
            .Configure((appBuilder, routes, serviceProvider) =>
            {
                var contributors = serviceProvider.GetServices<global::Grove.Api.IApiContributor>()
                    .GroupBy(contributor => contributor.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Last());

                foreach (var contributor in contributors)
                {
                    var existing = routes.DataSources.ToList();
                    try
                    {
                        contributor.Map(routes.MapGroup(contributor.RoutePrefix));
                        foreach (var source in routes.DataSources.Except(existing))
                        {
                            _ = source.Endpoints;
                        }
                    }
                    catch (Exception)
                    {
                        // A contributor this harness cannot build (a service only the real host
                        // has) is left out, as the real mapper leaves it out and reports it.
                        foreach (var source in routes.DataSources.Except(existing).ToList())
                        {
                            routes.DataSources.Remove(source);
                        }
                    }
                }
            })
            .Configure(appBuilder =>
            {
                appBuilder.UseAuthorization();

                // Regression probe for the duplicate endpoint name crash. Runs inside the tenant
                // pipeline (after UseRouting) where the endpoint data source is fully populated, so
                // link generation by name exercises the same code path that used to throw an
                // 'InvalidOperationException' about duplicate endpoint names.
                appBuilder.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/__eptnametest")
                    {
                        var endpointName = context.Request.Query["name"].ToString();
                        var linkGenerator = context.RequestServices.GetRequiredService<global::Microsoft.AspNetCore.Routing.LinkGenerator>();

                        // Throws 'InvalidOperationException' when duplicate non-suppressed named
                        // endpoints exist (the bug being guarded against).
                        var path = linkGenerator.GetPathByName(context, endpointName, new { name = "test" });

                        await context.Response.WriteAsync(path ?? string.Empty);

                        return;
                    }

                    await next();
                });
            }));

        services.AddSingleton<IModuleNamesProvider, ModuleNamesProvider>();
    }

#pragma warning disable CA1822 // Mark members as static
    public void Configure(IApplicationBuilder app)
#pragma warning restore CA1822 // Mark members as static
    {
        app.UsePlatform();
    }

    private sealed class ModuleNamesProvider : IModuleNamesProvider
    {
        private readonly string[] _moduleNames;

        public ModuleNamesProvider()
        {
            var assembly = Assembly.Load(new AssemblyName(typeof(Program).Assembly.GetName().Name));
            _moduleNames = assembly.GetCustomAttributes<ModuleNameAttribute>().Select(m => m.Name).ToArray();
        }

        public IEnumerable<string> GetModuleNames()
        {
            return _moduleNames;
        }
    }
}
