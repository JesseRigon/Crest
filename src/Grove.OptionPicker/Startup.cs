using Grove.Apis;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Fluid;
using Grove.ContentTypes;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.OptionPicker.Api;
using Grove.Security.Permissions;
using Grove.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.OptionPicker;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // OptionPickerField: the reference mechanism for option sources. Providers keep the field
        // ignorant of what it is picking (content part lists, users, content items, ...).
        services.AddContentField<Fields.OptionPickerField>()
            .UseDisplayDriver<Drivers.OptionPickerFieldDisplayDriver>()
            .AddHandler<Handlers.OptionPickerFieldHandler>();
        services.AddScoped<Grove.Indexing.IContentFieldIndexHandler, Indexing.OptionPickerFieldIndexHandler>();
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
        services.AddSingleton<Indexing.OptionIndexPartitions>();
        services.AddDataMigration<Indexing.OptionPickerFieldIndexMigrations>();
        services.AddScopedIndexProvider<Indexing.OptionPickerFieldIndexProvider>();

        services.AddPermissionProvider<Permissions.OptionPickerPermissions>();

        // Content items as the one way to pick items (docs/server.md § 3): the retired content
        // picker field converts on migration and on recipe import; the server side of a content
        // item pick (allowed types, the owners' relationship handlers) runs on every save.
        services.AddDataMigration<ContentItems.ContentPickerFieldMigrations>();
        services.AddTransient<Grove.Recipes.Events.IRecipeEventHandler, ContentItems.ContentPickerRecipeMapping>();
        services.AddScoped<Grove.ContentManagement.Handlers.IContentSaveGuard, ContentItems.ContentPickerRelationshipGuard>();

        services.AddApiContributor<OptionSourcesApi>();
        services.AddApiContributor<OptionPickerAttachmentsApi>();
    }
}

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddObjectGraphType<Fields.OptionPickerField, GraphQL.OptionPickerFieldQueryObjectType>();
}

// Liquid reads an option picker field and its display model (Model.SelectedIds, Model.Items).
[RequireFeatures("Grove.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<Fields.OptionPickerField>();
            options.MemberAccessStrategy.Register<Drivers.OptionPickerFieldViewModel>();
            options.MemberAccessStrategy.Register<Grove.ContentFields.ViewModels.DisplayLinkItem>();
        });
}
