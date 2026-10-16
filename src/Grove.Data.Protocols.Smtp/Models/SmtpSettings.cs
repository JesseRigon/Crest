using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Data.Protocols.Smtp;

/// <summary>
/// The SMTP provider's section of Settings › Email. Holds no secret: the server, its security
/// and its credential are the picked connection's (the Connections screen).
/// </summary>
public class SmtpSettings
{
    [Display(Name = "Enable the SMTP provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Default sender", Description = "The address messages are sent from when they name none.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string DefaultSender { get; set; }

    [Display(Name = "Connection", Description = "The SMTP connection messages are sent through; its server and credential are set on the Connections screen.", Prompt = "Select a connection", Order = 2)]
    [Setting(Options = typeof(SmtpConnectionSettingOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; }
}
