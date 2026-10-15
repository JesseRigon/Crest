using Grove.Apis;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Fluid;
using Grove.Contents.Types;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Contents.OptionPicker.Api;
using Grove.Security.Permissions;
using Grove.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.OptionPicker;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // OptionPickerField: the reference mechanism for option sources. Providers keep the field
        // ignorant of what it is picking (content part lists, users, content items, ...).
        services.AddContentField<global::Grove.Fields.OptionPickerField>()
            .UseDisplayDriver<Drivers.OptionPickerFieldDisplayDriver>()
            .AddHandler<Handlers.OptionPickerFieldHandler>();
        services.AddScoped<Grove.Data.Indexing.IContentFieldIndexHandler, global::Grove.Data.Indexing.OptionPickerFieldIndexHandler>();
        services.AddScoped<IOptionSourceProvider, UserOptionSourceProvider>();
        services.AddScoped<IOptionSourceProvider, ContentItemOptionSourceProvider>();
        services.AddScoped<IOptionSourceProvider, TimeZoneOptionSourceProvider>();
        services.AddScoped<IOptionSourceProvider, CultureOptionSourceProvider>();
        services.AddScoped<OptionPickerFieldKeyResolver>();

        // Parent id -> key resolution behind cascading filters and field-visibility conditions.
        services.AddScoped<OptionParentValueResolver>();
        services.AddScoped<IFieldComparisonValueResolver, OptionPickerComparisonValueResolver>();

        // Per-content-type index partitioning: modules register the high-volume types that get
        // their own table, and the catch-all partition skips them.
        services.AddSingleton<global::Grove.Data.Indexing.OptionIndexPartitions>();
        services.AddDataMigration<global::Grove.Data.Indexing.OptionPickerFieldIndexMigrations>();
        services.AddScopedIndexProvider<global::Grove.Data.Indexing.OptionPickerFieldIndexProvider>();

        services.AddPermissionProvider<global::Grove.Permissions.OptionPickerPermissions>();

        // Content items as the one way to pick items (docs/server.md § 3): the retired content
        // picker field converts on migration and on recipe import; the server side of a content
        // item pick (allowed types, the owners' relationship handlers) runs on every save.
        services.AddDataMigration<ContentItems.ContentPickerFieldMigrations>();
        services.AddTransient<Grove.Infrastructure.Recipes.Events.IRecipeEventHandler, ContentItems.ContentPickerRecipeMapping>();
        services.AddScoped<Grove.Contents.Management.Handlers.IContentSaveGuard, ContentItems.ContentPickerRelationshipGuard>();

        services.AddApiContributor<OptionSourcesApi>();
        services.AddApiContributor<OptionPickerAttachmentsApi>();
    }
}

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddObjectGraphType<global::Grove.Fields.OptionPickerField, GraphQL.OptionPickerFieldQueryObjectType>();
}

// Liquid reads an option picker field and its display model (Model.SelectedIds, Model.Items).
[RequireFeatures("Grove.Display.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<global::Grove.Fields.OptionPickerField>();
            options.MemberAccessStrategy.Register<Drivers.OptionPickerFieldViewModel>();
            options.MemberAccessStrategy.Register<Grove.Contents.Fields.ViewModels.DisplayLinkItem>();
        });
}
