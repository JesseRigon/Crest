using Grove.DisplayManagement;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentFields.Drivers;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Handlers;
using Grove.ContentFields.Indexing;
using Grove.ContentFields.Indexing.SQL;
using Grove.ContentFields.Services;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.ContentTypes.Editors;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Indexing;
using Grove.Localization;
using Grove.Modules;
using Grove.Users;

namespace Grove.ContentFields;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The field displays' data-localized labels (docs/blazor-display.md rework step 7).
        services.AddShapeTableProvider<FieldDisplayLabels>();

        services.AddHtmlServices();
        
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddScoped<IJSLocalizer, ContentFieldsJSLocalizer>();

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<BooleanField>();
            o.MemberAccessStrategy.Register<DisplayBooleanFieldViewModel>();
            o.MemberAccessStrategy.Register<HtmlField>();
            o.MemberAccessStrategy.Register<DisplayHtmlFieldViewModel>();
            o.MemberAccessStrategy.Register<LinkField>();
            o.MemberAccessStrategy.Register<DisplayLinkFieldViewModel>();
            o.MemberAccessStrategy.Register<NumericField>();
            o.MemberAccessStrategy.Register<DisplayNumericFieldViewModel>();
            o.MemberAccessStrategy.Register<TextField>();
            o.MemberAccessStrategy.Register<DisplayTextFieldViewModel>();
            o.MemberAccessStrategy.Register<DateTimeField>();
            o.MemberAccessStrategy.Register<DisplayDateTimeFieldViewModel>();
            o.MemberAccessStrategy.Register<DateField>();
            o.MemberAccessStrategy.Register<DisplayDateFieldViewModel>();
            o.MemberAccessStrategy.Register<TimeField>();
            o.MemberAccessStrategy.Register<DisplayTimeFieldViewModel>();
            o.MemberAccessStrategy.Register<MultiTextField>();
            o.MemberAccessStrategy.Register<DisplayMultiTextFieldViewModel>();
            o.MemberAccessStrategy.Register<UserPickerField>();
            o.MemberAccessStrategy.Register<DisplayUserPickerFieldViewModel>();
            o.MemberAccessStrategy.Register<ContentPickerField>();
            o.MemberAccessStrategy.Register<DisplayContentPickerFieldViewModel>();
        });

        // Boolean Field
        services.AddContentField<BooleanField>()
            .UseDisplayDriver<BooleanFieldDisplayDriver>();
        services.AddScoped<IContentFieldIndexHandler, BooleanFieldIndexHandler>();

        // Text Field
        services.AddContentField<TextField>()
            .UseDisplayDriver<TextFieldDisplayDriver>()
            .AddHandler<TextFieldHandler>();
        services.AddScoped<IContentFieldIndexHandler, TextFieldIndexHandler>();

        // Html Field
        services.AddContentField<HtmlField>()
            .UseDisplayDriver<HtmlFieldDisplayDriver>();
        services.AddScoped<IContentFieldIndexHandler, HtmlFieldIndexHandler>();

        // Link Field
        services.AddContentField<LinkField>()
            .UseDisplayDriver<LinkFieldDisplayDriver>()
            .AddHandler<LinkFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, LinkFieldIndexHandler>();

        // MultiText Field
        services.AddContentField<MultiTextField>()
            .UseDisplayDriver<MultiTextFieldDisplayDriver>()
            .AddHandler<MultiTextFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, MultiTextFieldIndexHandler>();

        // Numeric Field
        services.AddContentField<NumericField>()
            .UseDisplayDriver<NumericFieldDisplayDriver>()
            .AddHandler<NumericFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, NumericFieldIndexHandler>();

        // DateTime Field
        services.AddContentField<DateTimeField>()
            .UseDisplayDriver<DateTimeFieldDisplayDriver>()
            .AddHandler<DateTimeFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, DateTimeFieldIndexHandler>();

        // Date Field
        services.AddContentField<DateField>()
            .UseDisplayDriver<DateFieldDisplayDriver>()
            .AddHandler<DateFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, DateFieldIndexHandler>();

        // Time Field
        services.AddContentField<TimeField>()
            .UseDisplayDriver<TimeFieldDisplayDriver>()
            .AddHandler<TimeFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, TimeFieldIndexHandler>();

        // Video field
        services.AddContentField<YoutubeField>()
            .UseDisplayDriver<YoutubeFieldDisplayDriver>()
            .AddHandler<YoutubeFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, YoutubeFieldIndexHandler>();

        // Content picker field
        services.AddContentField<ContentPickerField>()
            .UseDisplayDriver<ContentPickerFieldDisplayDriver>()
            .AddHandler<ContentPickerFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, ContentPickerFieldIndexHandler>();
        services.AddScoped<IContentPickerResultProvider, DefaultContentPickerResultProvider>();
        // The picker's server side on every save path: allowed target types and the
        // relationship handlers field owners register (docs/content-items.md).
        services.AddScoped<IContentSaveGuard, ContentPickerRelationshipGuard>();

        // Migration, can be removed in a future release.
        services.AddDataMigration<Migrations>();
    }
}

[RequireFeatures("Grove.ContentLocalization")]
public sealed class LocalizationSetContentPickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentField<LocalizationSetContentPickerField>()
            .UseDisplayDriver<LocalizationSetContentPickerFieldDisplayDriver>()
            .AddHandler<LocalizationSetContentPickerFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, LocalizationSetContentPickerFieldIndexHandler>();
    }
}

[Feature("Grove.ContentFields.Indexing.SQL")]
public sealed class IndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Indexing.SQL.Migrations>();
        services.AddScopedIndexProvider<TextFieldIndexProvider>();
        services.AddScopedIndexProvider<BooleanFieldIndexProvider>();
        services.AddScopedIndexProvider<NumericFieldIndexProvider>();
        services.AddScopedIndexProvider<DateTimeFieldIndexProvider>();
        services.AddScopedIndexProvider<DateFieldIndexProvider>();
        services.AddScopedIndexProvider<ContentPickerFieldIndexProvider>();
        services.AddScopedIndexProvider<TimeFieldIndexProvider>();
        services.AddScopedIndexProvider<LinkFieldIndexProvider>();
        services.AddScopedIndexProvider<HtmlFieldIndexProvider>();
        services.AddScopedIndexProvider<MultiTextFieldIndexProvider>();
    }
}

[RequireFeatures(UserConstants.Features.Users)]
public sealed class UserPickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<UserPickerField>();
            o.MemberAccessStrategy.Register<DisplayUserPickerFieldViewModel>();
            o.MemberAccessStrategy.Register<DisplayUserPickerFieldUserNamesViewModel>();
        });

        services.AddContentField<UserPickerField>()
            .UseDisplayDriver<UserPickerFieldDisplayDriver>(d => !string.Equals(d, "UserNames", StringComparison.OrdinalIgnoreCase))
            .UseDisplayDriver<UserPickerFieldUserNamesDisplayDriver>(d => string.Equals(d, "UserNames", StringComparison.OrdinalIgnoreCase))
            .AddHandler<UserPickerFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, UserPickerFieldIndexHandler>();
        services.AddScoped<IUserPickerResultProvider, DefaultUserPickerResultProvider>();
    }
}

[Feature("Grove.ContentFields.Indexing.SQL.UserPicker")]
public sealed class UserPickerSqlIndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<UserPickerMigrations>();
        services.AddScopedIndexProvider<UserPickerFieldIndexProvider>();
    }
}
