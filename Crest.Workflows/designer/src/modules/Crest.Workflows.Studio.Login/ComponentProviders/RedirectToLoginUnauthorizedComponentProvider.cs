using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Extensions;
using Crest.Workflows.Studio.Login.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Login.ComponentProviders;

/// <inheritdoc />
public class RedirectToLoginUnauthorizedComponentProvider : IUnauthorizedComponentProvider
{
    /// <inheritdoc />
    public RenderFragment GetUnauthorizedComponent()
    {
        return builder => builder.CreateComponent<RedirectToLogin>();
    }
}