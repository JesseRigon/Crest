using Crest.Migrations;
using Crest.Models;
using Crest.Navigation;
using Crest.Services;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Liquid;
using Fluid;
using Crest.Navigation;

namespace Crest.ContentPartLists;

// Content Part Lists - the tenant-editable enum system (see the host repo's
// the host's module inventory, Tier 0). Modules declare the sets they own and consume
// them by logical key; tenants relabel/reorder/hide at runtime. Extracted from
// Crest.Server into its own feature so tenants opt in rather than getting it with
// the Crest core - consumers (consuming modules) declare it as a manifest
// dependency instead.
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<CrestContentPartListPart>();
        services.AddContentPart<CrestOptionPart>();
        services.AddDataMigration<CrestContentPartListMigrations>();
        services.AddScoped<Crest.Data.Global.Lists.IGlobalListReader, Crest.Data.Global.Lists.GlobalListReader>();
        services.AddScoped<IContentPartListService, ContentPartListService>();
        services.AddScoped<IContentPartListImporter, ContentPartListImporter>();

        // Culture -> measurement system -> preferred units (CLDR-derived data);
        // stateless, so a singleton.
        services.AddSingleton<IMeasurementPreferenceService, MeasurementPreferenceService>();

        // Plugs Content Part Lists into Crest.Server's picker infrastructure as one source
        // provider among several (users and content items live in Crest.Server).
        services.AddScoped<IOptionSourceProvider, ContentPartListSourceProvider>();


        // Teaches content groups the "list" entry kind (an Content part list by key).
        services.AddScoped<Crest.ContentGroups.IContentGroupEntryResolver, ContentPartListGroupEntryResolver>();
    }
}

// option_list / option_label over the same service the API wraps (docs/content-items.md).
[RequireFeatures("Crest.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(options => options.MemberAccessStrategy.Register<CrestOptionModel>());
        services.AddLiquidFilter<Liquid.OptionListFilter>("option_list");
        services.AddLiquidFilter<Liquid.OptionLabelFilter>("option_label");
    }
}
