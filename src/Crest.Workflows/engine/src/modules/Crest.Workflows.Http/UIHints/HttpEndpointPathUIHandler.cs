using System.Reflection;
using Crest.Workflows;
using Crest.Workflows.UIHints;
using Crest.Workflows.UIHints.SingleLine;

namespace Crest.Workflows.Http.UIHints;

/// <summary>
/// Provides additional options for the Path input field.
/// </summary>
public class HttpEndpointPathUIHandler(IHttpEndpointBasePathProvider httpEndpointBasePathProvider) : PropertyUIHandlerBase
{
    /// <inheritdoc />
    public override ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default)
    {
        var completeBaseUrl = httpEndpointBasePathProvider.GetBasePath();

        return new(new Dictionary<string, object>
        {
            [InputUIHints.SingleLine] = new SingleLineProps
            {
                AdornmentText = completeBaseUrl,
                EnableCopyAdornment = true
            },
            ["Refresh"] = true
        });
    }
}