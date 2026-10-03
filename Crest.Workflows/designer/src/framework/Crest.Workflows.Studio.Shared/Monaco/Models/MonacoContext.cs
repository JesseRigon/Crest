using BlazorMonaco.Editor;
using Crest.Workflows.Api.Client.Resources.Scripting.Models;

namespace Crest.Workflows.Studio.Models;

/// <summary>
/// Represents a context for working with the Monaco editor.
/// </summary>
public record MonacoContext(StandaloneCodeEditor Editor, ExpressionDescriptor ExpressionDescriptor, IDictionary<string, object> CustomProperties);