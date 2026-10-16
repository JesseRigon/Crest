namespace Grove.Data.Protocols.Smtp;

/// <summary>The tenant SMTP provider's runtime options, read from <see cref="SmtpSettings"/>.</summary>
public class SmtpOptions
{
    public bool IsEnabled { get; set; }

    public string DefaultSender { get; set; }

    /// <summary>The <c>smtp</c> connection messages are sent through.</summary>
    public string ConnectionKey { get; set; }
}
