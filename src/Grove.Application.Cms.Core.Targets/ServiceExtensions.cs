using Grove.ResourceManagement.TagHelpers;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceExtensions
{
    /// <summary>
    /// Adds Grove CMS services to the application.
    /// </summary>
    public static PlatformBuilder AddPlatformCms(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddPlatform()

            .AddCommands()

            .AddSecurity()
            .AddApiProblemDetails()
            .AddMvc()
            .AddIdGeneration()
            .AddEmailAddressValidator()
            .AddPhoneFormatValidator()
            .AddHtmlSanitizer()
            .AddSetupFeatures("Grove.Setup")

            .AddDataAccess()
            .AddDataStorage()
            .AddBackgroundService()
            .AddScripting()

            .AddTheming()
            .AddGlobalFeatures("Grove.Liquid.Core")
            .AddCaching();

        // PlatformBuilder is not available in Grove.ResourceManagement as it has to
        // remain independent from Grove.
        builder.ConfigureServices(s =>
        {
            s.AddResourceManagement();

            s.AddTagHelpers<LinkTagHelper>();
            s.AddTagHelpers<MetaTagHelper>();
            s.AddTagHelpers<ResourcesTagHelper>();
            s.AddTagHelpers<ScriptTagHelper>();
            s.AddTagHelpers<StyleTagHelper>();
        });

        return builder;
    }

    /// <summary>
    /// Adds Grove CMS services to the application and let the app change the
    /// default tenant behavior and set of features through a configure action.
    /// </summary>
    public static IServiceCollection AddPlatformCms(this IServiceCollection services, Action<PlatformBuilder> configure)
    {
        var builder = services.AddPlatformCms();

        configure?.Invoke(builder);

        return services;
    }
}
