using FastEndpoints;

namespace Crest.Workflows.Abstractions;

/// <summary>
/// An endpoint that maps a request to a response.
/// </summary>
public abstract class WorkflowsEndpointWithMapper<TRequest, TMapper> : EndpointWithMapper<TRequest, TMapper> where TMapper : class, IRequestMapper where TRequest : notnull
{
    protected void ConfigurePermissions(params string[] permissions)
    {
        if (!EndpointSecurityOptions.SecurityIsEnabled)
            AllowAnonymous();
        else
            Permissions(new[] { PermissionNames.All }.Concat(permissions).ToArray());
    }
}

public abstract class WorkflowsEndpointWithoutRequest : EndpointWithoutRequest
{
    protected void ConfigurePermissions(params string[] permissions)
    {
        if (!EndpointSecurityOptions.SecurityIsEnabled)
            AllowAnonymous();
        else
            Permissions(new[] { PermissionNames.All }.Concat(permissions).ToArray());
    }
}

public abstract class WorkflowsEndpointWithoutRequest<TResponse> : EndpointWithoutRequest<TResponse> where TResponse : notnull
{
    protected void ConfigurePermissions(params string[] permissions)
    {
        if (!EndpointSecurityOptions.SecurityIsEnabled)
            AllowAnonymous();
        else
            Permissions(new[] { PermissionNames.All }.Concat(permissions).ToArray());
    }
}

public class WorkflowsEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse> where TRequest : notnull, new() where TResponse : notnull
{
    protected void ConfigurePermissions(params string[] permissions)
    {
        if (!EndpointSecurityOptions.SecurityIsEnabled)
            AllowAnonymous();
        else
            Permissions(new[] { PermissionNames.All }.Concat(permissions).ToArray());
    }
}

public class WorkflowsEndpoint<TRequest, TResponse, TMapper> : Endpoint<TRequest, TResponse, TMapper> where TRequest : notnull, new() where TResponse : notnull where TMapper : class, IMapper, new()
{
    protected void ConfigurePermissions(params string[] permissions)
    {
        if (!EndpointSecurityOptions.SecurityIsEnabled)
            AllowAnonymous();
        else
            Permissions(new[] { PermissionNames.All }.Concat(permissions).ToArray());
    }
}

public class WorkflowsEndpoint<TRequest> : Endpoint<TRequest> where TRequest : notnull, new()
{
    protected void ConfigurePermissions(params string[] permissions)
    {
        if (!EndpointSecurityOptions.SecurityIsEnabled)
            AllowAnonymous();
        else
            Permissions(new[] { PermissionNames.All }.Concat(permissions).ToArray());
    }
}