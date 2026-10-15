using System.Reflection;
using Grove.Workflows;
using Grove.Workflows.UIHints;
using Grove.Workflows.UIHints.SingleLine;

namespace Grove.Workflows.Http.UIHints;

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