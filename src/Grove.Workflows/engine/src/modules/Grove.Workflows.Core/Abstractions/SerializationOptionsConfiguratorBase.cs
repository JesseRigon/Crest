using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Grove.Workflows.Common;

namespace Grove.Workflows;

/// <summary>
/// A base class for <see cref="ISerializationOptionsConfigurator"/> implementations.
/// </summary>
public abstract class SerializationOptionsConfiguratorBase : ISerializationOptionsConfigurator
{
    /// <inheritdoc />
    public virtual void Configure(JsonSerializerOptions options)
    {
    }

    /// <inheritdoc />
    public virtual IEnumerable<Action<JsonTypeInfo>> GetModifiers()
    {
        yield break;
    }
}