using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Indexing;
using Crest.Localization;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Seo.Drivers;
using Crest.Seo.Indexes;
using Crest.Seo.Models;
using Crest.Seo.Services;
using Crest.SeoMeta.Settings;

namespace Crest.Seo;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddScoped<IJSLocalizer, SeoJSLocalizer>();

        services.AddContentPart<SeoMetaPart>()
            .UseDisplayDriver<SeoMetaPartDisplayDriver>()
            .AddHandler<SeoMetaPartHandler>();

        services.AddScoped<IContentDisplayDriver, SeoContentDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SeoMetaPartSettingsDisplayDriver>();

        // This must be last, and the module dependent on Contents so this runs after the part handlers.
        services.AddScoped<IContentHandler, SeoMetaSettingsHandler>();
        services.AddScoped<IDocumentIndexHandler, SeoMetaPartContentIndexHandler>();

        services.AddPermissionProvider<SeoPermissionProvider>();
        services.AddSiteDisplayDriver<RobotsSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
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
