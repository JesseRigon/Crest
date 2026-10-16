using System.ComponentModel.DataAnnotations;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Infrastructure.Settings;

namespace Grove.Data.Protocols.Pop;

/// <summary>The POP3 receive provider's section of Settings › Email. Holds no secret.</summary>
public class PopSettings
{
    [Display(Name = "Enable the POP3 receive provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Connection", Description = "The POP3 connection mail is read through; its server and credential are set on the Connections screen.", Prompt = "Select a connection", Order = 1)]
    [Setting(Options = typeof(PopConnectionSettingOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; }
}

/// <summary>The tenant's <c>pop</c> connections.</summary>
public sealed class PopConnectionSettingOptions(IConnectionRegistry registry)
    : MailConnectionSettingOptions(registry, ConnectorProtocols.Pop);
