using Fluid;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Descriptors.ShapeTemplateStrategy;
using Grove.DisplayManagement.Liquid;
using Grove.DisplayManagement.Liquid.Filters;
using Grove.DisplayManagement.Liquid.TagHelpers;
using Grove.DisplayManagement.Liquid.Tags;
using Grove.DisplayManagement.Razor;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.Liquid;

namespace Microsoft.Extensions.DependencyInjection;

public static class LiquidCoreServices
{
    public static IServiceCollection AddLiquidCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<LiquidViewParser>();
        services.Configure<FluidParserOptions>(options =>
        {
            var configuration = ShellScope.Services.GetRequiredService<IShellConfiguration>();
            configuration.GetSection("Grove_Liquid").Bind(options);

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
