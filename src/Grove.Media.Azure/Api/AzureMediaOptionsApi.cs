#nullable enable

using Grove.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Media.Azure.Api;

/// <summary>
/// The Azure Blob media store's options, read-only (they come from the host configuration), for
/// the media options page; the connection string is never sent, only whether one is set.
/// </summary>
public sealed class AzureMediaOptionsApi : ApiContributor
{
    public override string Name => "AzureMediaOptions";

    public override string FeatureId => "Grove.Media.Azure.Storage";

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapGet("media/options/azure", ApiHandlers.Handle<AzureMediaOptionsHandlers>(handlers => handlers.GetAsync()));
}

public sealed class AzureMediaOptionsHandlers(IAuthorizationService authorization, IOptions<MediaBlobStorageOptions> options) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ViewAzureMediaOptions)) return Forbid();

        var value = options.Value;
        return Ok(new AzureMediaOptions(
            !string.IsNullOrWhiteSpace(value.ConnectionString),
            value.ContainerName,
            value.BasePath,
            value.CreateContainer,
            value.RemoveContainer,
            value.RemoveFilesFromBasePath));
    }
}

public sealed record AzureMediaOptions(bool HasConnectionString, string? ContainerName, string? BasePath, bool CreateContainer, bool RemoveContainer, bool RemoveFilesFromBasePath);
