using System.Text.Json.Nodes;

namespace Crest.Workflows.Studio.Workflows.Shared.Args;

/// Represents the event arguments when the designer path changes.
public record DesignerPathChangedArgs(JsonObject ParentActivity, JsonObject ContainerActivity);