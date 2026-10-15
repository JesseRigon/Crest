using Grove.Data.Storage.AzureBlob;

namespace Grove.Infrastructure.Shells.Azure.Configuration;

public class BlobShellStorageOptions : BlobStorageOptions
{
    public bool MigrateFromFiles { get; set; }
}
