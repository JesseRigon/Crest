using System.Text.Json;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;
using JetBrains.Annotations;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// A service that detects whether a JSON string is a workflow definition using a schema.
/// </summary>
[UsedImplicitly]
/// <summary>
/// Represents the schema workflow json detector.
/// </summary>
public class SchemaWorkflowJsonDetector : IWorkflowJsonDetector
{
    /// <inheritdoc />
    public bool IsWorkflowSchema(string json)
    {
        var jsonDocument = JsonDocument.Parse(json);
        var rootElement = jsonDocument.RootElement;

        if (!rootElement.TryGetProperty("$schema", out var schemaUrl))
            return false;

        if (schemaUrl.GetString()?.StartsWith("https://github.com/JesseRigon/Crest/blob/main/docs/workflows.md") == false)
            return false;

        return true;
    }
}