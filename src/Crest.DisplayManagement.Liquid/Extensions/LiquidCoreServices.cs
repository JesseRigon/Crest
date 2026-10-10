using Fluid;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Descriptors.ShapeTemplateStrategy;
using Crest.DisplayManagement.Liquid;
using Crest.DisplayManagement.Liquid.Filters;
using Crest.DisplayManagement.Liquid.TagHelpers;
using Crest.DisplayManagement.Liquid.Tags;
using Crest.DisplayManagement.Razor;
using Crest.Environment.Shell.Configuration;
using Crest.Environment.Shell.Scope;
using Crest.Liquid;

namespace Microsoft.Extensions.DependencyInjection;

public static class LiquidCoreServices
{
    public static IServiceCollection AddLiquidCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<LiquidViewParser>();
        services.Configure<FluidParserOptions>(options =>
        {
            var configuration = ShellScope.Services.GetRequiredService<IShellConfiguration>();
            configuration.GetSection("Crest_Liquid").Bind(options);

            // Always enable the use of functions, do not expose it as a configuration option.
            options.AllowFunctions = true;
        });

        services.AddSingleton<IAnchorTag, AnchorTag>();

        services.AddTransient<IConfigureOptions<TemplateOptions>, TemplateOptionsFileProviderSetup>();

        services.AddTransient<IConfigureOptions<LiquidViewOptions>, LiquidViewOptionsSetup>();

        services.AddTransient<IConfigureOptions<ShapeTemplateOptions>, LiquidShapeTemplateOptionsSetup>();

        services.AddSingleton<IApplicationFeatureProvider<ViewsFeature>, LiquidViewsFeatureProvider>();
        services.AddScoped<IRazorViewExtensionProvider, LiquidViewExtensionProvider>();
        services.AddSingleton<LiquidTagHelperFactory>();

        services.AddSingleton<IConfigureOptions<TemplateOptions>, TemplateOptionsConfigurations>();

        services.AddLiquidFilter<SanitizeHtmlFilter>("sanitize_html");

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddLiquidFilter<SupportedCulturesFilter>("supported_cultures");
#pragma warning restore CS0618 // Type or member is obsolete

        return services;
    }
}
