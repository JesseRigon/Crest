using System.Reflection;

namespace Crest.Workflows;

public abstract class PropertyUIHandlerBase : IPropertyUIHandler
{
    public virtual float Priority => 0;
    public abstract ValueTask<IDictionary<string, object>> GetUIPropertiesAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken = default);
}