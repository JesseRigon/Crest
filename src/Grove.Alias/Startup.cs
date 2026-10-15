using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Grove.Alias.Handlers;
using Grove.Alias.Indexes;
using Grove.Alias.Indexing;
using Grove.Alias.Models;
using Grove.Alias.Services;
using Grove.Alias.Settings;
using Grove.Alias.ViewModels;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.ContentTypes.Editors;
using Grove.Data;
using Grove.Data.Migration;
using Grove.DisplayManagement;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Modules;
using YesSql;

namespace Grove.Alias;

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
            .AddHandler<AliasPartHandler>();

        services.AddScoped<IContentPartIndexHandler, AliasPartIndexHandler>();
    }
}

[RequireFeatures("Grove.Contents")]
public sealed class ContentAliasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<ContentAliasShapeTableProvider>();
    }
}
