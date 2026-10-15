using Grove.Api;
using Grove.Media.Endpoints.Api;
using Microsoft.AspNetCore.Routing;

namespace Grove.Media.Api;

/// <summary>
/// <c>api/media/*</c>: the Media API the Vue media gallery and media field picker call (through
/// the NSwag-generated <c>MediaApiClient</c>): folders, items, copy/move/delete and uploads. Each
/// endpoint lives in <c>Endpoints/Api</c> and requires the <c>MediaApi</c> authorization policy
/// (cookie or bearer, as the Media API settings say); the group's antiforgery check covers the
/// writes (the token as the <c>RequestVerificationToken</c> header or, for the picker's upload
/// form, the <c>__RequestVerificationToken</c> field: both are read by the tenant's antiforgery).
/// </summary>
public sealed class MediaApi : IApiContributor
{
    public string Name => "MediaApi";

    public string FeatureId => "Grove.Media";

    public string RoutePrefix => "api/media";

    public void Map(IEndpointRouteBuilder group)
    {
        group.AddGetLocalizationsEndpoint()
            .AddGetPermittedStorageEndpoint()
            .AddGetDirectoryTreeEndpoint()
            .AddGetFoldersEndpoint()
            .AddGetMediaItemsEndpoint()
            .AddGetDirectoryContentEndpoint()
            .AddGetMediaItemEndpoint()
            .AddGetMediaFieldItemsEndpoint()
            .AddGetAllMediaItemsEndpoint()
            .AddCopyMediaEndpoint()
            .AddDeleteFolderEndpoint()
            .AddDeleteMediaEndpoint()
            .AddMoveMediaEndpoint()
            .AddDeleteMediaListEndpoint()
            .AddMoveMediaListEndpoint()
            .AddCreateFolderEndpoint()
            .AddUploadMediaEndpoint();
    }
}

/// <summary>
/// <c>api/media/TusFileInfo/{uploadId}</c>: a finished TUS upload's media item. It needs the TUS
/// metadata store, which only the <c>Grove.Media.Tus</c> feature registers.
/// </summary>
public sealed class MediaTusApi : IApiContributor
{
    public string Name => "MediaTus";

    public string FeatureId => "Grove.Media.Tus";

    public string RoutePrefix => "api/media";

    public void Map(IEndpointRouteBuilder group) => group.AddGetTusFileInfoEndpoint();
}
