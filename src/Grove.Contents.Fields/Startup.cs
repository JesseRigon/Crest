using Grove.Contents.Fields;
using Grove.Display.Management;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Fields.Drivers;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Handlers;
using Grove.Contents.Fields.Indexing;
using Grove.Contents.Fields.Indexing.SQL;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Data.Indexing;
using Grove.Data.Localization;
using Grove.Modules;
using Grove.Access.Users;

namespace Grove.Contents.Fields;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The field displays' data-localized labels (docs/blazor-display.md rework step 7).
        services.AddShapeTableProvider<FieldDisplayLabels>();

        services.AddHtmlServices();
        
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
        });

        // Boolean Field
        services.AddFieldType<BooleanField, BooleanFieldSettings>()
            .UseDisplayDriver<BooleanFieldDisplayDriver>();
        services.AddScoped<IContentFieldIndexHandler, BooleanFieldIndexHandler>();

        // Text Field
        services.AddFieldType<TextField, TextFieldSettings>()
            .UseDisplayDriver<TextFieldDisplayDriver>()
            .AddHandler<TextFieldHandler>();
        services.AddScoped<IContentFieldIndexHandler, TextFieldIndexHandler>();

        // Html Field
        services.AddFieldType<HtmlField, HtmlFieldSettings>()
            .UseDisplayDriver<HtmlFieldDisplayDriver>()
            .AddHandler<HtmlFieldHandler>();
        services.AddScoped<IContentFieldIndexHandler, HtmlFieldIndexHandler>();

        // Link Field
        services.AddFieldType<LinkField, LinkFieldSettings>()
            .UseDisplayDriver<LinkFieldDisplayDriver>()
            .AddHandler<LinkFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, LinkFieldIndexHandler>();

        // MultiText Field
        services.AddFieldType<MultiTextField, MultiTextFieldSettings>()
            .UseDisplayDriver<MultiTextFieldDisplayDriver>()
            .AddHandler<MultiTextFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, MultiTextFieldIndexHandler>();

        // Numeric Field
        services.AddFieldType<NumericField, NumericFieldSettings>()
            .UseDisplayDriver<NumericFieldDisplayDriver>()
            .AddHandler<NumericFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, NumericFieldIndexHandler>();

        // DateTime Field
        services.AddFieldType<DateTimeField, DateTimeFieldSettings>()
            .UseDisplayDriver<DateTimeFieldDisplayDriver>()
            .AddHandler<DateTimeFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, DateTimeFieldIndexHandler>();

        // Date Field
        services.AddFieldType<DateField, DateFieldSettings>()
            .UseDisplayDriver<DateFieldDisplayDriver>()
            .AddHandler<DateFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, DateFieldIndexHandler>();

        // Time Field
        services.AddFieldType<TimeField, TimeFieldSettings>()
            .UseDisplayDriver<TimeFieldDisplayDriver>()
            .AddHandler<TimeFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, TimeFieldIndexHandler>();

        // Video field
        services.AddFieldType<YoutubeField, YoutubeFieldSettings>()
            .UseDisplayDriver<YoutubeFieldDisplayDriver>()
            .AddHandler<YoutubeFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, YoutubeFieldIndexHandler>();

        // Migration, can be removed in a future release.
        services.AddDataMigration<Migrations>();
    }
}

[Feature("Grove.Contents.Fields.Indexing.SQL")]
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
        services.AddScopedIndexProvider<TimeFieldIndexProvider>();
        services.AddScopedIndexProvider<LinkFieldIndexProvider>();
        services.AddScopedIndexProvider<HtmlFieldIndexProvider>();
        services.AddScopedIndexProvider<MultiTextFieldIndexProvider>();
    }
}
