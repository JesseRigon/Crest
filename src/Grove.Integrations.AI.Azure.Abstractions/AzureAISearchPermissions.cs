using Grove.Security.Permissions;

namespace Grove.Integrations.AI.Azure;

public static class AzureAISearchPermissions
{
    public static readonly Permission ManageAzureAISearchISettings = new("ManageAzureAISearchISettings", "Manage Azure AI Search Settings");
}
