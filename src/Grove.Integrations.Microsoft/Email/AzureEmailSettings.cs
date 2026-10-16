using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Microsoft.Email;

/// <summary>
/// The Azure Communication Services provider's section of Settings › Email. Holds no secret:
/// the connection string is the connection's credential (the Connections screen).
/// </summary>
public class AzureEmailSettings
{
    [Display(Name = "Enable the Azure Communication Services provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Default sender", Description = "A sender address of the resource's verified domain.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string DefaultSender { get; set; }

    [Display(Name = "Connection", Description = "Its connection string is set on the Connections screen.", Prompt = "Select a connection", Order = 2)]
    [Setting(Options = typeof(AzureEmailConnectionOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; } = AzureCommunicationServices.EmailConnectionKey;
}
