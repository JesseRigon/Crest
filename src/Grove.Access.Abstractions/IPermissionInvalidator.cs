namespace Grove.Access;

/// <summary>
/// Tells the tenant's signed-in admin clients that permissions changed (a role, a user's roles, the
/// permission model), so they reload what they show; served by the permissions hub.
/// </summary>
public interface IPermissionInvalidator
{
    Task InvalidateTenantAsync(CancellationToken cancellationToken = default);
}
