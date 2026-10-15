using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Html.Drivers;
using Grove.Html.Handlers;
using Grove.Html.Indexing;
using Grove.Html.Models;
using Grove.Html.Settings;
using Grove.Html.ViewModels;
using Grove.Indexing;
using Grove.Modules;

namespace Grove.Html;

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
