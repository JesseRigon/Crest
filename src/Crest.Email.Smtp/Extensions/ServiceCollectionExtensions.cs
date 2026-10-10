using Microsoft.Extensions.DependencyInjection;
using Crest.Email.Smtp.Services;

namespace Crest.Email.Smtp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmailProvider(this IServiceCollection services)
    {
        services.AddEmailProviderOptionsConfiguration<SmtpProviderOptionsConfigurations>();

        return services;
    }
}
