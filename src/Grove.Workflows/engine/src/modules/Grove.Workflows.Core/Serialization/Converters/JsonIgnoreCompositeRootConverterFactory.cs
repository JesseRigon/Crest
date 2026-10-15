using System.Text.Json;
using System.Text.Json.Serialization;
using Grove.Workflows.Serialization.Helpers;

namespace Grove.Workflows.Serialization.Converters;

/// <summary>
/// A <see cref="JsonConverterFactory"/> that creates <see cref="JsonIgnoreCompositeRootConverter"/> instances.
/// </summary>
public class JsonIgnoreCompositeRootConverterFactory(ActivityWriter activityWriter) : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeof(IActivity).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new JsonIgnoreCompositeRootConverter(activityWriter);
    }
}