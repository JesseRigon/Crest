namespace Grove.Workflows.Http;

/// <summary>
/// The HTTP endpoint routes the workflows of the current tenant answer: one per path of every
/// <see cref="Activities.HttpEndpoint"/> trigger and of every bookmark waiting on one. The host
/// serves each as an endpoint of its route table and sends the requests to
/// <see cref="Services.HttpWorkflowRequestHandler"/>.
/// </summary>
public interface IHttpEndpointRoutes
{
    Task<IReadOnlyList<HttpRouteData>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Told when the set of HTTP endpoint triggers or bookmarks changed (a workflow published or
/// retracted, a run suspended at or resumed from an HTTP endpoint), so the host reloads the routes.
/// </summary>
public interface IHttpEndpointRoutesObserver
{
    void RoutesChanged();
}
