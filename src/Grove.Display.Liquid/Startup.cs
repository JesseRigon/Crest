using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Display.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.Data.Indexing;
using Grove.Display.Liquid.Endpoints.Scripts;
using Grove.Display.Liquid.Filters;
using Grove.Display.Liquid.Services;
using Grove.Modules;

namespace Grove.Display.Liquid;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddGetIntellisenseScriptEndpoint();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILiquidTemplateManager, LiquidTemplateManager>();

        services.Configure<TemplateOptions>(options =>
        {
            options.Filters.AddFilter("html_class", (input, _, _) => ValueTask.FromResult<FluidValue>(new StringValue(Grove.Utilities.StringExtensions.HtmlClassify(input.ToStringValue()))));

            // Used to provide a factory to return a value based on a property name that is unknown at registration time.
            options.MemberAccessStrategy.Register<LiquidPropertyAccessor, FluidValue>((obj, name) => obj.GetValueAsync(name));

            // When a property of a 'JsonObject' value is accessed, try to look into its properties.
            options.MemberAccessStrategy.Register<JsonObject, object>((source, name) => source[name]);

            // When a property of a 'JsonDynamicObject' value is accessed, try to look into its properties.
            options.MemberAccessStrategy.Register<JsonDynamicObject, object>((json, name) => json[name]);

            // Convert JToken to FluidValue
            options.ValueConverters.Add(x =>
            {
                return x switch
                {
                    JsonObject o => new ObjectValue(o),
                    JsonDynamicObject o => new ObjectValue((JsonObject)o),
                    JsonValue o => o.GetObjectValue(),
                    JsonDynamicValue o => ((JsonValue)(o.Node)).GetObjectValue(),
                    DateTime d => new ObjectValue(d),
                    _ => null

                };
            });

            options.Filters.AddFilter("json", JsonFilter.Json);
            options.Filters.AddFilter("jsonparse", JsonParseFilter.JsonParse);
        })
        .AddLiquidFilter<LocalTimeZoneFilter>("local")
        .AddLiquidFilter<UtcTimeZoneFilter>("utc")
        .AddLiquidFilter<SlugifyFilter>("slugify")
        .AddLiquidFilter<LiquidFilter>("liquid")
        .AddLiquidFilter<ContentUrlFilter>("href")
        .AddLiquidFilter<AbsoluteUrlFilter>("absolute_url")
        .AddLiquidFilter<LocalizeFilter>("t")
        .AddLiquidFilter<EncryptFilter>("encrypt")
        .AddLiquidFilter<DecryptFilter>("decrypt");

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}

[Feature("Grove.Display.Liquid.Core")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The shared Liquid language: one parser and the language configuration every template uses.
        services.AddSingleton<LiquidParser>();
        services.Configure<FluidParserOptions>(options =>
        {
            var configuration = ShellScope.Services.GetRequiredService<IShellConfiguration>();
            configuration.GetSection("Grove_Liquid").Bind(options);

            // Always enable the use of functions, do not expose it as a configuration option.
            options.AllowFunctions = true;
        });
        services.AddSingleton<IConfigureOptions<TemplateOptions>, LiquidTemplateOptionsSetup>();

        services.AddLiquidFilter<SanitizeHtmlFilter>("sanitize_html");
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddLiquidFilter<SupportedCulturesFilter>("supported_cultures");
#pragma warning restore CS0618 // Type or member is obsolete
    }
}

[RequireFeatures("Grove.Shortcodes")]
public sealed class ShortcodesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<ShortcodeFilter>("shortcode");
    }
}

