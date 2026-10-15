using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Environment.Options;
using Grove.Sms.Azure.Models;
using Grove.Sms.Azure.Services;

namespace Grove.Sms.Azure;

public static class SmsProviderExtensions
{
    public static IServiceCollection AddAzureSmsProvider(this IServiceCollection services)
        => services.AddSmsProviderOptionsConfiguration<AzureSmsProviderOptionsConfigurations>()
        .AddSignalOptionsChangeTokenSource<AzureSmsOptions>()
        .AddTransient<IConfigureOptions<AzureSmsOptions>, AzureSmsOptionsConfiguration>();
}
