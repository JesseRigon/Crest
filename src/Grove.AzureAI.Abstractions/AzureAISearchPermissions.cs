using Grove.Security.Permissions;

namespace Grove.AzureAI;

public static class AzureAISearchPermissions
{
    public static readonly Permission ManageAzureAISearchISettings = new("ManageAzureAISearchISettings", "Manage Azure AI Search Settings");
}
