using Microsoft.Extensions.DependencyInjection;
using Grove.Email.Smtp.Services;

namespace Grove.Email.Smtp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmailProvider(this IServiceCollection services)
    {
        services.AddEmailProviderOptionsConfiguration<SmtpProviderOptionsConfigurations>();

        return services;
    }
}
