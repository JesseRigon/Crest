namespace Crest.Access;

/// <summary>
/// The tenant's permission version: bumped by every role, binding and policy write, carried
/// by every cached caller state and compared on use, so a decision is always against current
/// rights.
/// </summary>
public interface IPermissionVersion
{
    Task<long> GetAsync(CancellationToken cancellationToken = default);

    Task BumpAsync(CancellationToken cancellationToken = default);
}
