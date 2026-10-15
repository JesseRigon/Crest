using System.ComponentModel.DataAnnotations;

namespace Grove.Integrations.AI.Azure.Models;

public enum AzureAIAuthenticationType
{
    Default = 0,
    [Display(Name = "API Key")]
    ApiKey = 1,
    [Display(Name = "Managed Identity")]
    ManagedIdentity = 2,
}
