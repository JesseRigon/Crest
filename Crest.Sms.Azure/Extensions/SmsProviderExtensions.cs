using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Environment.Options;
using Crest.Sms.Azure.Models;
using Crest.Sms.Azure.Services;

namespace Crest.Sms.Azure;

public static class SmsProviderExtensions
{
    public static IServiceCollection AddAzureSmsProvider(this IServiceCollection services)
        => services.AddSmsProviderOptionsConfiguration<AzureSmsProviderOptionsConfigurations>()
        .AddSignalOptionsChangeTokenSource<AzureSmsOptions>()
        .AddTransient<IConfigureOptions<AzureSmsOptions>, AzureSmsOptionsConfiguration>();
}
