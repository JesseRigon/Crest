#nullable enable

using Grove.Api;
using Grove.FileStorage.AmazonS3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Media.AmazonS3.Api;

/// <summary>
/// The Amazon S3 media store's options, read-only (they come from the host configuration), for
/// the media options page, with the configuration errors when it is not configured.
/// </summary>
public sealed class AmazonS3MediaOptionsApi : ApiContributor
{
    public override string Name => "AmazonS3MediaOptions";

    public override string FeatureId => "Grove.Media.AmazonS3";

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapGet("media/options/amazon-s3", ApiHandlers.Handle<AmazonS3MediaOptionsHandlers>(handlers => handlers.GetAsync()));
}

public sealed class AmazonS3MediaOptionsHandlers(IAuthorizationService authorization, IOptions<AwsStorageOptions> options) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ViewAmazonS3MediaOptions)) return Forbid();

        var value = options.Value;
        var errors = value.Validate().Select(result => result.ErrorMessage ?? string.Empty).ToArray();
        return Ok(new AmazonS3MediaOptions(value.BucketName, value.BasePath, value.CreateBucket, errors));
    }
}

public sealed record AmazonS3MediaOptions(string? BucketName, string? BasePath, bool CreateBucket, string[] Errors);
