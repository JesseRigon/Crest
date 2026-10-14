using Crest.Api;
using Crest.Media.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Crest.Media.Api;

/// <summary>The media library's change notifications (<c>/hubs/media</c>, a path of its own).</summary>
public sealed class MediaHubApi : ApiContributor
{
    public override string Name => "MediaHub";

    public override string FeatureId => "Crest.Media.SignalR";

    public override string RoutePrefix => "hubs";

    public override void Map(IEndpointRouteBuilder group) => group.MapHub<MediaHub>("media");
}
