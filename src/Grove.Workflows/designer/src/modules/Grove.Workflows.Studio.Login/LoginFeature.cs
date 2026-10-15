using Grove.Workflows.Studio.Abstractions;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Login.Components;

namespace Grove.Workflows.Studio.Login;

/// <summary>
/// Represents the login feature module that provides authentication functionality for the Grove.Workflows Studio application.
/// </summary>
public class LoginFeature(IAppBarService appBarService) : FeatureBase
{
    /// <inheritdoc/>
    public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        appBarService.AddComponent<LoginState>();
        return base.InitializeAsync(cancellationToken);
    }
}