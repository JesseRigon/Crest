using Crest.Workflows.Memory;

namespace Crest.Workflows;

/// <summary>
/// Represents a variable and its value.
/// </summary>
public record ResolvedVariable(Variable Variable, object? Value);