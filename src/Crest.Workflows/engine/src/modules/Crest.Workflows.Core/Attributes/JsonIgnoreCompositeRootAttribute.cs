using Crest.Workflows.Serialization.Converters;

namespace Crest.Workflows.Attributes;

/// <summary>
/// Used by <see cref="JsonIgnoreCompositeRootConverter"/> to indicate that the property should be expanded into a JSON object.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class JsonIgnoreCompositeRootAttribute : Attribute
{
}