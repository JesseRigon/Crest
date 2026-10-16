namespace Grove.Data.Protocols.Smtp;

/// <summary>How the host's configured SMTP delivery (<see cref="DefaultSmtpOptions"/>) handles a message.</summary>
public enum SmtpDeliveryMethod
{
    /// <summary>Not delivered by the host's configuration: messages go through the tenant's connection.</summary>
    Network,

    /// <summary>Written as an <c>.eml</c> file to the pickup directory.</summary>
    SpecifiedPickupDirectory,
}
