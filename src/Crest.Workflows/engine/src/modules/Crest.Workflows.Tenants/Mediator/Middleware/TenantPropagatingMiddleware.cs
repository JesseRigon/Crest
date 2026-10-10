using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Mediator.Middleware.Command;
using Crest.Workflows.Mediator.Middleware.Command.Contracts;
using JetBrains.Annotations;

namespace Crest.Workflows.Tenants.Mediator.Middleware;

/// <summary>
/// Middleware that ensures tenant context is propagated through the request pipeline.
/// </summary>
[UsedImplicitly]
public class TenantPropagatingMiddleware(CommandMiddlewareDelegate next, ITenantScopeFactory tenantScopeFactory, ITenantService tenantService) : ICommandMiddleware
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(CommandContext context)
    {
        if (context.Headers.TryGetValue(TenantHeaders.TenantIdKey, out var tenantIdVal))
        {
            var tenantId = (string)tenantIdVal;
            var tenant = await tenantService.FindAsync(tenantId);
            await using var tenantScope = tenantScopeFactory.CreateScope(tenant);
            await next(context);
            return;
        }

        await next(context);
    }
}