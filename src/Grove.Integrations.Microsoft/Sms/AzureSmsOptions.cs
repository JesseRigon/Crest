namespace Grove.Integrations.Microsoft.Sms;

/// <summary>The Azure SMS provider's runtime options, read from <see cref="AzureSmsSettings"/>.</summary>
public class AzureSmsOptions
{
    public bool IsEnabled { get; set; }

    public string PhoneNumber { get; set; }

    public string ConnectionKey { get; set; }
}
