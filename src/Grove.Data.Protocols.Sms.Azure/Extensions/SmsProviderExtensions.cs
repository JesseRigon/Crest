using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Environment.Options;
using Grove.Data.Protocols.Sms.Azure.Models;
using Grove.Data.Protocols.Sms.Azure.Services;
using Grove.Communications.Sms;

namespace Grove.Data.Protocols.Sms.Azure;

public static class SmsProviderExtensions
{
    public static IServiceCollection AddAzureSmsProvider(this IServiceCollection services)
        => services.AddSmsProviderOptionsConfiguration<AzureSmsProviderOptionsConfigurations>()
        .AddSignalOptionsChangeTokenSource<AzureSmsOptions>()
        .AddTransient<IConfigureOptions<AzureSmsOptions>, AzureSmsOptionsConfiguration>();
}
