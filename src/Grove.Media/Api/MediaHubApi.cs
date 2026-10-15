using Grove.Api;
using Grove.Media.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Grove.Media.Api;

/// <summary>The media library's change notifications (<c>/hubs/media</c>, a path of its own).</summary>
public sealed class MediaHubApi : ApiContributor
{
    public override string Name => "MediaHub";

    public override string FeatureId => "Grove.Media.SignalR";

    public override string RoutePrefix => "hubs";

    public override void Map(IEndpointRouteBuilder group) => group.MapHub<MediaHub>("media");
}
