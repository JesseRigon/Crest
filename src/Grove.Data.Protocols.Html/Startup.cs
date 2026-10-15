using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Data.Protocols.Html.Drivers;
using Grove.Data.Protocols.Html.Handlers;
using Grove.Data.Protocols.Html.Indexing;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Settings;
using Grove.Data.Protocols.Html.ViewModels;
using Grove.Data.Indexing;
using Grove.Modules;

namespace Grove.Data.Protocols.Html;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHtmlServices();
        
        services.Configure<TemplateOptions>(o => o.MemberAccessStrategy.Register<HtmlBodyPartViewModel>());

        // Body Part
        services.AddContentPart<HtmlBodyPart>()
            .UseDisplayDriver<HtmlBodyPartDisplayDriver>()
            .AddHandler<HtmlBodyPartHandler>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentPartIndexHandler, HtmlBodyPartIndexHandler>();
    }
}
