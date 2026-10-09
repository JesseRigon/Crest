using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Crest.Alias.Drivers;
using Crest.Alias.Handlers;
using Crest.Alias.Indexes;
using Crest.Alias.Indexing;
using Crest.Alias.Models;
using Crest.Alias.Services;
using Crest.Alias.Settings;
using Crest.Alias.ViewModels;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.ContentTypes.Editors;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Modules;
using YesSql;

namespace Crest.Alias;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AliasPartViewModel>();

            o.MemberAccessStrategy.Register<LiquidContentAccessor, LiquidPropertyAccessor>("Alias", (obj, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;

                return new LiquidPropertyAccessor(liquidTemplateContext, async (alias, context) =>
                {
                    var session = context.Services.GetRequiredService<ISession>();

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
                    var contentItem = await session.Query<ContentItem, AliasPartIndex>(x =>
                        x.Published && x.Alias == alias.ToLowerInvariant()).FirstOrDefaultAsync();
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

                    if (contentItem == null)
                    {
                        return NilValue.Instance;
                    }

                    var contentManager = context.Services.GetRequiredService<IContentManager>();
                    contentItem = await contentManager.LoadAsync(contentItem);

                    return new ObjectValue(contentItem);
                });
            });
        });

        services.AddScoped<AliasPartIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<AliasPartIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<AliasPartIndexProvider>());

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentHandleProvider, AliasPartContentHandleProvider>();

        // Identity Part
        services.AddContentPart<AliasPart>()
            .UseDisplayDriver<AliasPartDisplayDriver>()
            .AddHandler<AliasPartHandler>();

        services.AddScoped<IContentPartIndexHandler, AliasPartIndexHandler>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, AliasPartSettingsDisplayDriver>();
    }
}

[RequireFeatures("Crest.Contents")]
public sealed class ContentAliasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<ContentAliasShapeTableProvider>();
    }
}

[RequireFeatures("Crest.Widgets")]
public sealed class WidgetAliasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<WidgetAliasShapeTableProvider>();
    }
}
