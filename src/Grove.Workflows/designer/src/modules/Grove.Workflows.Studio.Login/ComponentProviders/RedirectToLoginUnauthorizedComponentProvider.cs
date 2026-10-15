using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Extensions;
using Grove.Workflows.Studio.Login.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.Login.ComponentProviders;

/// <inheritdoc />
public class RedirectToLoginUnauthorizedComponentProvider : IUnauthorizedComponentProvider
{
    /// <inheritdoc />
    public RenderFragment GetUnauthorizedComponent()
    {
        return builder => builder.CreateComponent<RedirectToLogin>();
    }
}