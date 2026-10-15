using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Infrastructure.Setup.Components;
using Grove.Infrastructure.Setup.Services;
using Grove.Data.Localization;
using Grove.Modules;

namespace Grove.Infrastructure.Setup;

public sealed class Startup : StartupBase
{
    private readonly string _defaultCulture = CultureInfo.InstalledUICulture.Name;

    private string[] _supportedCultures =
    [
        "ar",
        "cs",
        "de",
        "el",
        "en",
        "es",
        "fa",
        "fr",
        "it",
        "ja",
        "pl",
        "pt-BR",
        "ru",
        "sv",
        "tr",
        "vi",
        "zh-CN",
        "zh-TW",
        "zh-Hans-CN",
        "zh-Hant-TW"
    ];

    public Startup(IShellConfiguration shellConfiguration)
    {
        var configurationSection = shellConfiguration.GetSection("Grove_Setup");

        _defaultCulture = configurationSection["DefaultCulture"] ?? _defaultCulture;
        _supportedCultures = configurationSection.GetSection("SupportedCultures").Get<List<string>>()?.ToArray() ?? _supportedCultures;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPortableObjectLocalization(options => options.ResourcesPath = "Localization");
        services.Replace(ServiceDescriptor.Singleton<ILocalizationFileLocationProvider, ModularPoFileLocationProvider>());

        services.AddSetup();

        // The installation pages: static SSR Razor components, no interactivity.
        services.AddRazorComponents();
        services.AddScoped<SetupInstallation>();

        services.Configure<IdentityOptions>(options =>
        {
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._+";
            options.User.RequireUniqueEmail = true;
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var localizationOptions = serviceProvider.GetService<IOptions<RequestLocalizationOptions>>().Value;
        var cultureOptions = serviceProvider.GetService<IOptions<CultureOptions>>().Value;

        localizationOptions.CultureInfoUseUserOverride = !cultureOptions.IgnoreSystemSettings;

        if (!string.IsNullOrEmpty(_defaultCulture))
        {
            localizationOptions.SetDefaultCulture(_defaultCulture);

            _supportedCultures = _supportedCultures.Union(new[] { _defaultCulture }).ToArray();
        }

        if (_supportedCultures?.Length > 0)
        {
            localizationOptions
                .AddSupportedCultures(_supportedCultures)
                .AddSupportedUICultures(_supportedCultures);
        }

        app.UseRequestLocalization(localizationOptions);

        // The installation pages run in the setup shell, before a database exists: only an
        // uninitialized tenant serves them. Their form posts validate the antiforgery token.
        if (serviceProvider.GetRequiredService<ShellSettings>().IsUninitialized())
        {
            app.UseAntiforgery();
            routes.MapRazorComponents<SetupDocument>();
        }
    }
}
