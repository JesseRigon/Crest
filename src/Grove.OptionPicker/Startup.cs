using Grove.ContentManagement;
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
        services.AddContentField<Fields.OptionPickerField>();
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

        // The option sources API gates on viewing content part lists.
        services.AddPermissionProvider<Permissions.ContentPartListPermissions>();

        services.AddApiContributor<OptionSourcesApi>();
        services.AddApiContributor<OptionPickerAttachmentsApi>();
    }
}
