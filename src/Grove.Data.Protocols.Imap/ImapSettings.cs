using System.ComponentModel.DataAnnotations;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Infrastructure.Settings;

namespace Grove.Data.Protocols.Imap;

/// <summary>The IMAP receive provider's section of Settings › Email. Holds no secret.</summary>
public class ImapSettings
{
    [Display(Name = "Enable the IMAP receive provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Connection", Description = "The IMAP connection mail is read through; its server and credential are set on the Connections screen.", Prompt = "Select a connection", Order = 1)]
    [Setting(Options = typeof(ImapConnectionSettingOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; }

    [Display(Name = "Folder", Description = "The folder read; the connection's folder (INBOX by default) when empty.", Order = 2)]
    [Setting(VisibleWhen = nameof(IsEnabled), Placeholder = "INBOX")]
    public string Folder { get; set; }
}

/// <summary>The tenant's <c>imap</c> connections.</summary>
public sealed class ImapConnectionSettingOptions(IConnectionRegistry registry)
    : MailConnectionSettingOptions(registry, ConnectorProtocols.Imap);
