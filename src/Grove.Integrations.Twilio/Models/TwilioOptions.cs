namespace Grove.Integrations.Twilio.Models;

/// <summary>The Twilio SMS provider's runtime options, read from <see cref="TwilioSettings"/>.</summary>
public class TwilioOptions
{
    public bool IsEnabled { get; set; }

    /// <summary>The sending phone number.</summary>
    public string PhoneNumber { get; set; }

    /// <summary>The account messages are created under.</summary>
    public string AccountSID { get; set; }

    /// <summary>The connection the provider sends through.</summary>
    public string ConnectionKey { get; set; }
}
