using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Builders;

namespace Grove.Communications.Email.Services;

public sealed class DefaultEmailReceiveProviderResolver(
    IOptionsMonitor<EmailOptions> emailOptions,
    IOptionsMonitor<EmailReceiveProviderOptions> providerOptions,
    IServiceProvider serviceProvider) : IEmailReceiveProviderResolver
{
    public ValueTask<IEmailReceiveProvider> GetAsync(string providerName = null)
    {
        var name = string.IsNullOrEmpty(providerName) ? emailOptions.CurrentValue.DefaultReceiveProviderName : providerName;
        if (string.IsNullOrEmpty(name))
        {
            return ValueTask.FromResult<IEmailReceiveProvider>(null);
        }

        if (providerOptions.CurrentValue.Providers.TryGetValue(name, out var provider))
        {
            return ValueTask.FromResult(serviceProvider.CreateInstance<IEmailReceiveProvider>(provider.Type));
        }

        throw new InvalidEmailProviderException(name);
    }
}
