using Amazon.S3;
using Amazon.S3.Model;
using Grove.Environment.Shell.Configuration;
using Grove.FileStorage;
using Grove.FileStorage.AmazonS3;
using Grove.Media.Files;
using Grove.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Media.AmazonS3.Services;

/// <summary>
/// File objects' bytes in Amazon S3 (docs/media.md § 3, requirement 11): the platform's
/// <see cref="AwsFileStore"/> in the media bucket under the files base path
/// (<c>Grove_Media_AmazonS3:FilesBasePath</c>; by default the media base path followed by
/// <c>-files</c>, or <c>grove-files</c>), through <see cref="FileStoreMediaStorageProvider"/>, so the
/// media library's own tree never lists them. A quarantined upload's time-limited reference
/// for an offsite scanner (requirement 94) is a presigned GET URL.
/// </summary>
public static class S3MediaStorage
{
    public const string ProviderName = "amazon-s3";

    public const string DefaultBasePath = "grove-files";

    public static IMediaStorageProvider Create(IServiceProvider serviceProvider)
    {
        var media = serviceProvider.GetRequiredService<IOptions<AwsStorageOptions>>().Value;
        var configured = serviceProvider.GetRequiredService<IShellConfiguration>()[$"{AmazonS3Constants.ConfigSections.AmazonS3}:FilesBasePath"];
        var basePath = !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim('/')
            : string.IsNullOrWhiteSpace(media.BasePath?.Trim('/')) ? DefaultBasePath : media.BasePath.Trim('/') + "-files";

        var options = new AwsStorageOptions
        {
            BucketName = media.BucketName,
            BasePath = basePath,
            AwsOptions = media.AwsOptions,
        };

        var client = serviceProvider.GetRequiredService<IAmazonS3>();
        var store = new AwsFileStore(serviceProvider.GetRequiredService<IClock>(), options, client);
        return new FileStoreMediaStorageProvider(ProviderName, store, async (path, lifetime, _) =>
        {
            var url = await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = options.BucketName,
                Key = store.Combine(basePath, path),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow + lifetime,
            });

            return new Uri(url);
        });
    }
}
