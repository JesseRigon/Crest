using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Contracts;

/// <summary>
/// Provides the <see cref="RenderFragment"/> to display the login page.
/// </summary>
public interface IUnauthorizedComponentProvider
{
    /// <summary>
    /// Returns the <see cref="RenderFragment"/> to display the login page.
    /// </summary>
    RenderFragment GetUnauthorizedComponent();
}