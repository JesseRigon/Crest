using Crest.Workflows.Http.Bookmarks;
using JetBrains.Annotations;

namespace Crest.Workflows.Http.Contexts;

[UsedImplicitly]
public record HttpEndpointRouteProviderContext(HttpEndpointBookmarkPayload Payload, string? TenantId, CancellationToken CancellationToken);