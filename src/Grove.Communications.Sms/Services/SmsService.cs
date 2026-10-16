using Microsoft.Extensions.Localization;
using Grove.Communications.Delivery;
using Grove.Infrastructure;
using Grove.Modules;

namespace Grove.Communications.Sms.Services;

public class SmsService : ISmsService
{
    private readonly ISmsProviderResolver _smsProviderResolver;
    private readonly IClock _clock;
    private readonly IDeliveryLog _deliveryLog;
    private ISmsProvider _provider;

    protected readonly IStringLocalizer S;

    public SmsService(
        ISmsProviderResolver smsProviderResolver,
        IStringLocalizer<SmsService> stringLocalizer,
        IClock clock,
        IDeliveryLog deliveryLog = null)
    {
        _smsProviderResolver = smsProviderResolver;
        S = stringLocalizer;
        _clock = clock;
        _deliveryLog = deliveryLog;
    }

    /// <summary>
    /// Sends the specified SMS message by using the configured default SMS provider.
    /// </summary>
    /// <param name="message">The SMS message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> describing whether the SMS was sent successfully.</returns>
    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        _provider ??= await _smsProviderResolver.GetAsync();

        if (_provider is null)
        {
            return Result.Failed(S["SMS settings must be configured before an SMS message can be sent."]);
        }

        var result = await _provider.SendAsync(message, cancellationToken);

        // The delivery log (Grove.Communications' delivery log feature), when it is on.
        await _deliveryLog.RecordAsync(DeliveryChannels.Sms, _provider.Name?.Name, message.To, null, result, _clock.UtcNow, cancellationToken);

        return result;
    }
}
