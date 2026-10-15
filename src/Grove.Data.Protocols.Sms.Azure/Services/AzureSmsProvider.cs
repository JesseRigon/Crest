using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Protocols.Sms.Azure.Models;

namespace Grove.Data.Protocols.Sms.Azure.Services;

public sealed class AzureSmsProvider : AzureSmsProviderBase<AzureSmsOptions>
{
    public const string TechnicalName = "Azure";

    public AzureSmsProvider(
        IOptionsMonitor<AzureSmsOptions> options,
        IPhoneFormatValidator phoneFormatValidator,
        ILogger<AzureSmsProvider> logger,
        IStringLocalizer<AzureSmsProvider> stringLocalizer)
        : base(options, phoneFormatValidator, logger, stringLocalizer)
    {
    }

    public override LocalizedString Name
        => S["Azure Communication Services"];
}
