using Crest.Workflows.Studio.Components;
using Crest.Workflows.Studio.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Shell.ComponentProviders;

/// <summary>
/// Provides a <see cref="RenderFragment"/> that displays default error component.
/// </summary>
public class DefaultErrorComponentProvider : IErrorComponentProvider
{
    /// <summary>
    /// Returns a <see cref="RenderFragment"/> that displays default error component.
    /// </summary>
    public RenderFragment GetErrorComponent(Exception context)
    {
        return builder =>
        {
            builder.OpenComponent<Error>(0);
            builder.AddAttribute(1, nameof(Error.Context), context);
            builder.CloseComponent();
        };
    }
}