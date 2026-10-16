namespace Grove.Integrations.Microsoft.Email;

/// <summary>The Azure e-mail provider's runtime options, read from <see cref="AzureEmailSettings"/>.</summary>
public class AzureEmailOptions
{
    public bool IsEnabled { get; set; }

    public string DefaultSender { get; set; }

    public string ConnectionKey { get; set; }
}
