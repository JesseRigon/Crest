using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.AzureAI.Models;

public class AzureAISearchDefaultSettings
{
    public const string GroupId = "azureAISearch";

    [Display(Name = "Use custom Azure Search AI settings", Description = "Check to configure a custom Azure Search AI instance; required when the host does not configure one.", Order = 0)]
    public bool UseCustomConfiguration { get; set; }

    [Display(Name = "Authentication Type", Order = 2)]
    [Setting(VisibleWhen = nameof(UseCustomConfiguration))]
    public AzureAIAuthenticationType AuthenticationType { get; set; }

    [Display(Name = "Endpoint", Prompt = "https://[search service name].search.windows.net", Order = 1)]
    [DataType(DataType.Url)]
    [Setting(VisibleWhen = nameof(UseCustomConfiguration))]
    public string Endpoint { get; set; }

    [Display(Name = "Api Key", Order = 3)]
    [Setting(Secret = true, ProtectedBy = "AzureAISearch", VisibleWhen = "AuthenticationType=ApiKey")]
    public string ApiKey { get; set; }

    [Display(Name = "Identity Client Id", Description = "Leaving identity client id blank will use system.", Order = 4)]
    [Setting(VisibleWhen = "AuthenticationType=ManagedIdentity")]
    public string IdentityClientId { get; set; }
}
