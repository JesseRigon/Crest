using System.Text.Json;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Api.Client.Shared.UIHints.SingleLine;

namespace Crest.Workflows.Studio.UIHints.Extensions;

/// <summary>
/// Provides extension methods for <see cref="InputDescriptor"/>.
/// </summary>
public static class InputDescriptorSingleLineExtensions
{
    /// <summary>
    /// Gets the <see cref="SingleLineProps"/> for the specified <see cref="InputDescriptor"/>.
    /// </summary>
    public static SingleLineProps GetSingleLineProps(this InputDescriptor descriptor)
    {
        var specifications = descriptor.UISpecifications;
        var props = specifications != null ? specifications.TryGetValue(InputUIHints.SingleLine, out var propsValue) ? propsValue is JsonElement value ? value : default : default : default;

        if (props.ValueKind == JsonValueKind.Undefined)
            return new SingleLineProps();

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        return props.Deserialize<SingleLineProps>(serializerOptions)!;
    }
}