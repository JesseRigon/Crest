using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Html.Drivers;
using Crest.Html.Handlers;
using Crest.Html.Indexing;
using Crest.Html.Models;
using Crest.Html.Settings;
using Crest.Html.ViewModels;
using Crest.Indexing;
using Crest.Modules;

namespace Crest.Html;

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

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, HtmlBodyPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, HtmlBodyPartTrumbowygEditorSettingsDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, HtmlBodyPartMonacoEditorSettingsDriver>();
        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentPartIndexHandler, HtmlBodyPartIndexHandler>();
    }
}
