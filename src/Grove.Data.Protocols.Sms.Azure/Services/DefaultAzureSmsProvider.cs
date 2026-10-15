using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Protocols.Sms.Azure.Models;

namespace Grove.Data.Protocols.Sms.Azure.Services;

public sealed class DefaultAzureSmsProvider : AzureSmsProviderBase<DefaultAzureSmsOptions>
{
    public const string TechnicalName = "DefaultAzure";

    public DefaultAzureSmsProvider(
        IOptionsMonitor<DefaultAzureSmsOptions> options,
        IPhoneFormatValidator phoneFormatValidator,
        ILogger<DefaultAzureSmsProvider> logger,
        IStringLocalizer<DefaultAzureSmsProvider> stringLocalizer)
        : base(options, phoneFormatValidator, logger, stringLocalizer)
    {
    }

    public override LocalizedString Name
        => S["Default Azure Communication Services"];
}
