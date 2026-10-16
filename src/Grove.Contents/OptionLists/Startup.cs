using Grove.Migrations;
using Grove.Models;
using Grove.Services;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Display.Liquid;
using Fluid;
using Grove.Security.Permissions;

namespace Grove.Contents.OptionLists;

// Option lists (docs/content-engine.md › Option lists): tenant-editable sets of options that modules
// declare and consume by key; tenants relabel, reorder and hide options at runtime. The
// Grove.Contents.OptionLists feature of this module; consumers declare it as a dependency.
[Feature(OptionListsFeature.Id)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentFieldSet<OptionListFieldSet>();
        services.AddContentFieldSet<OptionEntryFieldSet>();
        services.AddDataMigration<OptionListMigrations>();
        services.AddScoped<Grove.Data.Global.Lists.IGlobalListReader, Grove.Data.Global.Lists.GlobalListReader>();
        services.AddScoped<IOptionListService, OptionListService>();
        services.AddScoped<IOptionListImporter, OptionListImporter>();
        services.AddPermissionProvider<Grove.Permissions.OptionListPermissions>();

        // Culture -> measurement system -> preferred units (CLDR-derived data); stateless.
        services.AddSingleton<IMeasurementPreferenceService, MeasurementPreferenceService>();

        // Option lists as one option source among several (docs/server.md § 3).
        services.AddScoped<IOptionSourceProvider, OptionListSourceProvider>();

        // Teaches content groups the "optionlist" entry kind (an option list by key).
        services.AddScoped<Grove.ContentGroups.IContentGroupEntryResolver, OptionListGroupEntryResolver>();

        services.AddApiContributor<Api.OptionListsApi>();
        services.AddApiContributor<Api.GlobalListsApi>();
    }
}

// option_list / option_label over the same service the API wraps (docs/content-engine.md › Liquid over content).
[Feature(OptionListsFeature.Id)]
[RequireFeatures("Grove.Display.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(options => options.MemberAccessStrategy.Register<GroveOptionModel>());
        services.AddLiquidFilter<Liquid.OptionListFilter>("option_list");
        services.AddLiquidFilter<Liquid.OptionLabelFilter>("option_label");
    }
}

public static class OptionListsFeature
{
    public const string Id = "Grove.Contents.OptionLists";
}
