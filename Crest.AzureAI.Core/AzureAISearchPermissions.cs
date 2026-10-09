using Crest.Security.Permissions;

namespace Crest.AzureAI;

public static class AzureAISearchPermissions
{
    public static readonly Permission ManageAzureAISearchISettings = new("ManageAzureAISearchISettings", "Manage Azure AI Search Settings");
}
