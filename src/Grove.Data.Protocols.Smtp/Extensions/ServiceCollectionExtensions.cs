using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Communications.Email;

namespace Grove.Data.Protocols.Smtp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmailProvider(this IServiceCollection services)
    {
        services.AddEmailProviderOptionsConfiguration<SmtpProviderOptionsConfigurations>();

        return services;
    }
}
