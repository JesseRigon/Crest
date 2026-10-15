using Grove.Migrations;
using Grove.Models;
using Grove.Navigation;
using Grove.Services;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Liquid;
using Fluid;
using Grove.Navigation;

namespace Grove.ContentPartLists;

// Content Part Lists - the tenant-editable enum system (see the host repo's
// the host's module inventory, Tier 0). Modules declare the sets they own and consume
// them by logical key; tenants relabel/reorder/hide at runtime. Extracted from
// Grove.Server into its own feature so tenants opt in rather than getting it with
// the Grove core - consumers (consuming modules) declare it as a manifest
// dependency instead.
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<GroveContentPartListPart>();
        services.AddContentPart<GroveOptionPart>();
        services.AddDataMigration<GroveContentPartListMigrations>();
        services.AddScoped<Grove.Data.Global.Lists.IGlobalListReader, Grove.Data.Global.Lists.GlobalListReader>();
        services.AddScoped<IContentPartListService, ContentPartListService>();
        services.AddScoped<IContentPartListImporter, ContentPartListImporter>();

        // Culture -> measurement system -> preferred units (CLDR-derived data);
        // stateless, so a singleton.
        services.AddSingleton<IMeasurementPreferenceService, MeasurementPreferenceService>();

        // Plugs Content Part Lists into Grove.Server's picker infrastructure as one source
        // provider among several (users and content items live in Grove.Server).
        services.AddScoped<IOptionSourceProvider, ContentPartListSourceProvider>();


        // Teaches content groups the "list" entry kind (an Content part list by key).
        services.AddScoped<Grove.ContentGroups.IContentGroupEntryResolver, ContentPartListGroupEntryResolver>();
    }
}

// option_list / option_label over the same service the API wraps (docs/content-items.md).
[RequireFeatures("Grove.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(options => options.MemberAccessStrategy.Register<GroveOptionModel>());
        services.AddLiquidFilter<Liquid.OptionListFilter>("option_list");
        services.AddLiquidFilter<Liquid.OptionLabelFilter>("option_label");
    }
}
