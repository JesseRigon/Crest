using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Localization;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Shortcodes.Deployment;
using Crest.Shortcodes.Drivers;
using Crest.Shortcodes.Providers;
using Crest.Shortcodes.Recipes;
using Crest.Shortcodes.Services;
using Crest.Shortcodes.ViewModels;
using Sc = Shortcodes;

namespace Crest.Shortcodes;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ShortcodeViewModel>();

            o.MemberAccessStrategy.Register<Sc.Context, object>((obj, name) => obj[name]);

            o.ValueConverters.Add(x =>
            {
                return x switch
                {
                    // Prevent Context from being converted to an ArrayValue as it implements IEnumerable
                    Sc.Context c => new ObjectValue(c),
                    // Prevent Arguments from being converted to an ArrayValue as it implements IEnumerable
                    Sc.Arguments a => new ObjectValue(a),
                    _ => null
                };
            });

            o.MemberAccessStrategy.Register<Sc.Arguments, object>((obj, name) => obj.Named(name));
        });

        services.AddScoped<IShortcodeService, ShortcodeService>();
        services.AddScoped<IShortcodeDescriptorManager, ShortcodeDescriptorManager>();
        services.AddScoped<IShortcodeDescriptorProvider, ShortcodeOptionsDescriptorProvider>();
        services.AddScoped<IShortcodeContextProvider, DefaultShortcodeContextProvider>();

        services.AddOptions<ShortcodeOptions>();
        services.AddScoped<Sc.IShortcodeProvider, OptionsShortcodeProvider>();
        services.AddDisplayDriver<ShortcodeDescriptor, ShortcodeDescriptorDisplayDriver>();
    }
}

[Feature("Crest.Shortcodes.Templates")]
public sealed class ShortcodeTemplatesStartup : StartupBase
{
    // Register this first so the templates provide overrides for any code driven shortcodes.
    public override int Order => -10;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ShortcodeTemplatesManager>();
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IJSLocalizer, ShortcodesJSLocalizer>();

        services.AddRecipeExecutionStep<ShortcodeTemplateStep>();

        services.AddScoped<Sc.IShortcodeProvider, TemplateShortcodeProvider>();
        services.AddScoped<IShortcodeDescriptorProvider, ShortcodeTemplatesDescriptorProvider>();
    }
}

[RequireFeatures("Crest.Localization")]
public sealed class LocaleShortcodeProviderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShortcode<LocaleShortcodeProvider>("locale", d =>
        {
            d.DefaultValue = "[locale {language_code}] [/locale]";
            d.Hint = "Conditionally render content in the specified language";
            d.Usage =
@"[locale en]English Text[/locale][locale fr false]French Text[/locale]<br>
<table>
  <tr>
    <td>Args:</td>
    <td>lang, fallback</td>
  </tr>
</table>";
            d.Categories = ["Localization"];
        });
    }
}

[RequireFeatures("Crest.Deployment", "Crest.Shortcodes.Templates")]
public sealed class ShortcodeTemplatesDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllShortcodeTemplatesDeploymentSource, AllShortcodeTemplatesDeploymentStep, AllShortcodeTemplatesDeploymentStepDriver>();
    }
}
