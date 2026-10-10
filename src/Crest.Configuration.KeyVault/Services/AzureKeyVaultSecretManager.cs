using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace Crest.Configuration.KeyVault.Services;

public class AzureKeyVaultSecretManager : KeyVaultSecretManager
{
    public override string GetKey(KeyVaultSecret secret) =>
        secret.Name.Replace("---", "_").Replace("--", ":");
}
