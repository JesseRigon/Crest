using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Crest.FileStorage;
using Crest.FileStorage.AzureBlob;
using Crest.Media.Files;
using Crest.Modules;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Media.Azure.Services;

/// <summary>
/// File objects' bytes in Azure Blob Storage (docs/media.md § 3, requirement 11): the platform's
/// <see cref="BlobFileStore"/> under the files base path, through
/// <see cref="FileStoreMediaStorageProvider"/>. A quarantined upload's time-limited reference
/// for an offsite scanner (requirement 94) is a read-only SAS URL, when the connection string
/// carries an account key (otherwise the bytes are relayed).
/// </summary>
public static class AzureBlobMediaStorage
{
    public const string ProviderName = "azure-blob";

    public static IMediaStorageProvider Create(IServiceProvider serviceProvider)
    {
        var options = MediaBlobFilesStorageOptions.Resolve(
            serviceProvider.GetRequiredService<IOptions<MediaBlobFilesStorageOptions>>().Value,
            serviceProvider.GetRequiredService<IOptions<MediaBlobStorageOptions>>().Value);

        var store = new BlobFileStore(
            options,
            serviceProvider.GetRequiredService<IClock>(),
            serviceProvider.GetRequiredService<IContentTypeProvider>(),
            serviceProvider.GetRequiredService<ILogger<BlobFileStore>>());

        var container = new BlobContainerClient(options.ConnectionString, options.ContainerName);
        return new FileStoreMediaStorageProvider(ProviderName, store, (path, lifetime, _) =>
        {
            var blob = container.GetBlobClient(store.Combine(options.BasePath, path));
            return Task.FromResult(blob.CanGenerateSasUri
                ? blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow + lifetime)
                : null);
        });
    }
}
