using System.ComponentModel.DataAnnotations;
using Grove.Data.Connectors;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Microsoft.GraphMail;

/// <summary>
/// The Microsoft Graph send provider's section of Settings › Email. Mail goes out as the account
/// the connection's tenant grant signed in with; the grant is made on the Connections screen.
/// </summary>
public class GraphMailSettings
{
    [Display(Name = "Enable the Microsoft Graph provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Save to Sent Items", Description = "Keep a copy of each message in the account's Sent Items.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public bool SaveToSentItems { get; set; } = true;

    [Display(Name = "Connection", Description = "The Microsoft Graph connection; its tenant grant is made on the Connections screen.", Prompt = "Select a connection", Order = 2)]
    [Setting(Options = typeof(GraphConnectionOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; } = MicrosoftAuthenticationConstants.GraphConnectionKey;
}

/// <summary>The Microsoft Graph receive provider's section of Settings › Email.</summary>
public class GraphMailboxSettings
{
    [Display(Name = "Enable the Microsoft Graph mailbox", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Folder", Description = "The mail folder read: a well-known name (inbox) or a folder id.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled), Placeholder = "inbox")]
    public string Folder { get; set; }

    [Display(Name = "Connection", Description = "The Microsoft Graph connection; its tenant grant is made on the Connections screen.", Prompt = "Select a connection", Order = 2)]
    [Setting(Options = typeof(GraphConnectionOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; } = MicrosoftAuthenticationConstants.GraphConnectionKey;
}

/// <summary>The Microsoft Graph connections.</summary>
public sealed class GraphConnectionOptions(IConnectionRegistry registry)
    : AzureCommunicationConnectionOptions(registry, MicrosoftAuthenticationConstants.Features.Graph);
