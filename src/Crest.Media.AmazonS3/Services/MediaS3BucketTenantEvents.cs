using Amazon.S3;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell;
using Crest.FileStorage.AmazonS3;

namespace Crest.Media.AmazonS3.Services;

public sealed class MediaS3BucketTenantEvents : AwsTenantEventsBase
{
    public MediaS3BucketTenantEvents(
        ShellSettings shellSettings,
        IAmazonS3 amazonS3Client,
        IOptions<AwsStorageOptions> options,
        IStringLocalizer<MediaS3BucketTenantEvents> localizer,
        ILogger<MediaS3BucketTenantEvents> logger)
        : base(shellSettings, amazonS3Client, options.Value, localizer, logger)
    {
    }
}
