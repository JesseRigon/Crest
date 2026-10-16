using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp.Services;

namespace Grove.Data.Protocols.Smtp;

/// <summary>The tenant's <c>smtp</c> connections.</summary>
public sealed class SmtpConnectionSettingOptions(IConnectionRegistry registry)
    : MailConnectionSettingOptions(registry, ConnectorProtocols.Smtp);
