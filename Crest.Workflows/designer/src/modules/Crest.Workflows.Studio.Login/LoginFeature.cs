using Crest.Workflows.Studio.Abstractions;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Login.Components;

namespace Crest.Workflows.Studio.Login;

/// <summary>
/// Represents the login feature module that provides authentication functionality for the Crest.Workflows Studio application.
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