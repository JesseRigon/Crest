namespace Grove.Data.Protocols.Smtp;

/// <summary>The transport security of a mail server connection (its <c>security</c> setting).</summary>
public enum SmtpEncryptionMethod
{
    None = 0,
    SslTls = 1,
    StartTls = 2,

    /// <summary>TLS on connect for the implicit-TLS ports, STARTTLS when the server offers it otherwise.</summary>
    Auto = 3,
}
