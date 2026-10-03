using System.Text.Json;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Api.Client.Shared.UIHints.CodeEditor;

namespace Crest.Workflows.Studio.UIHints.Extensions;

/// <summary>
/// Provides extension methods for <see cref="InputDescriptor"/>.
/// </summary>
public static class InputDescriptorCodeEditorExtensions
{
    /// <summary>
    /// Gets the <see cref="CodeEditorOptions"/> for the specified <see cref="InputDescriptor"/>.
    /// </summary>
    public static CodeEditorOptions GetCodeEditorOptions(this InputDescriptor descriptor)
    {
        var specifications = descriptor.UISpecifications;
        var props = specifications != null ? specifications.TryGetValue(InputUIHints.CodeEditor, out var propsValue) ? propsValue is JsonElement value ? value : default : default : default;

        if (props.ValueKind == JsonValueKind.Undefined)
            return new CodeEditorOptions();

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        return props.Deserialize<CodeEditorOptions>(serializerOptions)!;
    }
}