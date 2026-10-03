using System.Text.Json;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Api.Client.Shared.UIHints.DropDown;

namespace Crest.Workflows.Studio.UIHints.Extensions;

/// <summary>
/// Provides extension methods for <see cref="InputDescriptor"/>.
/// </summary>
public static class InputDescriptorSelectListExtensions
{
    /// <summary>
    /// Gets the <see cref="SelectList"/> for the specified <see cref="InputDescriptor"/>.
    /// </summary>
    public static SelectList GetSelectList(this InputDescriptor descriptor)
    {
        var specifications = descriptor.UISpecifications;
        var props = specifications != null ? specifications.TryGetValue(InputUIHints.DropDown, out var propsValue) ? propsValue is JsonElement value ? value : default : default : default;

        if (props.ValueKind == JsonValueKind.Undefined)
            return new([]);

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var dropDownProps = props.Deserialize<DropDownProps>(serializerOptions);
        return dropDownProps?.SelectList ?? new SelectList([]);
    }
}