using System.ComponentModel.DataAnnotations;

namespace Grove.AzureAI.Models;

public enum AzureAIAuthenticationType
{
    Default = 0,
    [Display(Name = "API Key")]
    ApiKey = 1,
    [Display(Name = "Managed Identity")]
    ManagedIdentity = 2,
}
