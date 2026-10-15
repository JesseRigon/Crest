using Grove.Migrations;
using Grove.Models;
using Grove.Services;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Liquid;
using Fluid;
using Grove.Security.Permissions;

namespace Grove.Contents.PartLists;

// Content Part Lists (docs/content-items.md): tenant-editable sets of options that modules
// declare and consume by key; tenants relabel, reorder and hide options at runtime. The
// Grove.Contents.PartLists feature of this module; consumers declare it as a dependency.
[Feature(PartListsFeature.Id)]
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
        services.AddPermissionProvider<Grove.Permissions.ContentPartListPermissions>();

        // Culture -> measurement system -> preferred units (CLDR-derived data); stateless.
        services.AddSingleton<IMeasurementPreferenceService, MeasurementPreferenceService>();

        // Content part lists as one option source among several (docs/server.md § 3).
        services.AddScoped<IOptionSourceProvider, ContentPartListSourceProvider>();

        // Teaches content groups the "list" entry kind (a content part list by key).
        services.AddScoped<Grove.ContentGroups.IContentGroupEntryResolver, ContentPartListGroupEntryResolver>();
    }
}

// option_list / option_label over the same service the API wraps (docs/content-items.md).
[Feature(PartListsFeature.Id)]
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

public static class PartListsFeature
{
    public const string Id = "Grove.Contents.PartLists";
}
