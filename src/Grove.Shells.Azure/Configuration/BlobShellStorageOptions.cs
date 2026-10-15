using Grove.FileStorage.AzureBlob;

namespace Grove.Shells.Azure.Configuration;

public class BlobShellStorageOptions : BlobStorageOptions
{
    public bool MigrateFromFiles { get; set; }
}
