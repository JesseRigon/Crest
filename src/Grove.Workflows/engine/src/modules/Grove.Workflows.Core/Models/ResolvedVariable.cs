using Grove.Workflows.Memory;

namespace Grove.Workflows;

/// <summary>
/// Represents a variable and its value.
/// </summary>
public record ResolvedVariable(Variable Variable, object? Value);