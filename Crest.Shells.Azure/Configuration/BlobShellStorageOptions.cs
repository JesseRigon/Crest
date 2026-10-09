using Crest.FileStorage.AzureBlob;

namespace Crest.Shells.Azure.Configuration;

public class BlobShellStorageOptions : BlobStorageOptions
{
    public bool MigrateFromFiles { get; set; }
}
