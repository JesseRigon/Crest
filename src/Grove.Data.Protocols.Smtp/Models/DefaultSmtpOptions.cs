namespace Grove.Data.Protocols.Smtp;

/// <summary>
/// The host's pickup-directory delivery, from the shell configuration section
/// <c>Grove_Email_Smtp</c>: development and test hosts write each message as an <c>.eml</c> file
/// instead of sending it. Nothing leaves the host, so no connection is involved.
/// </summary>
public class DefaultSmtpOptions
{
    public const string ConfigurationSection = "Grove_Email_Smtp";

    public bool IsEnabled { get; set; }

    public string DefaultSender { get; set; }

    /// <summary><see cref="SmtpDeliveryMethod.SpecifiedPickupDirectory"/> turns the pickup delivery on.</summary>
    public SmtpDeliveryMethod DeliveryMethod { get; set; }

    /// <summary>A directory under <see cref="PickupDirectoryLocationBase"/>; resolved to the full path.</summary>
    public string PickupDirectoryLocation { get; set; }

    /// <summary>The base directory (a Liquid template over <c>AppData</c> and <c>ShellSettings</c>); the tenant's App_Data Emails folder by default.</summary>
    public string PickupDirectoryLocationBase { get; set; }

    public bool ConfigurationExists() =>
        !string.IsNullOrEmpty(DefaultSender) && DeliveryMethod == SmtpDeliveryMethod.SpecifiedPickupDirectory;
}
