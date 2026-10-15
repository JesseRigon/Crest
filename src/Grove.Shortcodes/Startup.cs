using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Data.Localization;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Shortcodes.Deployment;
using Grove.Shortcodes.Providers;
using Grove.Shortcodes.Recipes;
using Grove.Shortcodes.Services;
using Grove.Shortcodes.ViewModels;
using Sc = Shortcodes;

namespace Grove.Shortcodes;

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
        services.AddApiContributor<Api.ShortcodesApi>();
    }
}

[Feature("Grove.Shortcodes.Templates")]
public sealed class ShortcodeTemplatesStartup : StartupBase
{
    // Register this first so the templates provide overrides for any code driven shortcodes.
    public override int Order => -10;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ShortcodeTemplatesManager>();
        services.AddPermissionProvider<Permissions>();

        services.AddRecipeExecutionStep<ShortcodeTemplateStep>();

        // The shortcode templates page (Grove.Shortcodes.BlazorWasm) and its API.
        services.AddApiContributor<Api.ShortcodeTemplatesApi>();

        services.AddScoped<Sc.IShortcodeProvider, TemplateShortcodeProvider>();
        services.AddScoped<IShortcodeDescriptorProvider, ShortcodeTemplatesDescriptorProvider>();
    }
}

[RequireFeatures("Grove.Data.Localization")]
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

[RequireFeatures("Grove.Deployment", "Grove.Shortcodes.Templates")]
public sealed class ShortcodeTemplatesDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllShortcodeTemplatesDeploymentSource, AllShortcodeTemplatesDeploymentStep>();
    }
}
