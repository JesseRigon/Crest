using Microsoft.Extensions.Localization;
using Grove.Infrastructure;

namespace Grove.Communications.Email.Services;

public sealed class DefaultEmailReceiveService(
    IEmailReceiveProviderResolver resolver,
    IStringLocalizer<DefaultEmailReceiveService> S) : IEmailReceiveService
{
    public async Task<Result<EmailReceiveBatch>> ReceiveAsync(EmailReceiveRequest request, string providerName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = await resolver.GetAsync(providerName);
        if (provider is null)
        {
            return Result.Failed<EmailReceiveBatch>(S["A receive provider must be configured before mail can be received."]);
        }

        return await provider.ReceiveAsync(request, cancellationToken);
    }
}
