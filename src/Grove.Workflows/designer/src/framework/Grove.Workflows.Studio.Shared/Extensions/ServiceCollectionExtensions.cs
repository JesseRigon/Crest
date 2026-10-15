using Grove.Workflows.Studio.Branding;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Localization.Time;
using Grove.Workflows.Studio.Localization.Time.Providers;
using Grove.Workflows.Studio.Monaco.Handlers;
using Grove.Workflows.Studio.Services;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace Grove.Workflows.Studio.Extensions;

/// <summary>
/// Adds shared services to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds shared services to the service collection.
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddScoped<TypeDefinitionService>();
        
        // TODO: Move this to a new package; either specific to JS, or perhaps a general Monaco package that supports multiple languages.
        // We'll decide once we add support for more languages.
        services.AddScoped<IMonacoHandler, JavaScriptMonacoHandler>();

        // Time services.
        services.AddScoped<ITimeFormatter, DefaultTimeFormatter>();
        services.AddScoped<ITimeZoneProvider, UtcTimeZoneProvider>();
        services.AddScoped<IBrandingProvider, DefaultBrandingProvider>();

        // Required for the Radzen.Blazor.RadzenHtmlEditorLink component to work.
        services.AddRadzenComponents();

        return services;
    }
}