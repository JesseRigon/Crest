using Grove.Workflows.Http.Bookmarks;
using JetBrains.Annotations;

namespace Grove.Workflows.Http.Contexts;

[UsedImplicitly]
public record HttpEndpointRouteProviderContext(HttpEndpointBookmarkPayload Payload, string? TenantId, CancellationToken CancellationToken);