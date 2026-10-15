using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Data.Indexing;
using Grove.Data.Localization;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Seo.Drivers;
using Grove.Seo.Indexes;
using Grove.Seo.Models;
using Grove.Seo.Services;

namespace Grove.Seo;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();

        services.AddContentPart<SeoMetaPart>()
            .AddHandler<SeoMetaPartHandler>();

        services.AddScoped<IContentDisplayDriver, SeoContentDriver>();

        // This must be last, and the module dependent on Contents so this runs after the part handlers.
        services.AddScoped<IContentHandler, SeoMetaSettingsHandler>();
        services.AddScoped<IDocumentIndexHandler, SeoMetaPartContentIndexHandler>();

        services.AddPermissionProvider<SeoPermissionProvider>();
        services.AddPageSource<AdminPages>();
        services.AddSettingsGroup<RobotsSettings>(SeoConstants.RobotsSettingsGroupId, "Robots", SeoConstants.ManageSeoSettings,
            group => group.InMenu("seo", entryId: "seo-robots"));
        services.AddTransient<IRobotsProvider, SiteSettingsRobotsProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var pipeline = routes.CreateApplicationBuilder()
            .UseMiddleware<RobotsMiddleware>()
            .Build();

        var path = "/" + SeoConstants.RobotsFileName;

        routes.Map(path, pipeline);
        routes.Map($"{path}/{{**robotsPath}}", pipeline);
    }
}
