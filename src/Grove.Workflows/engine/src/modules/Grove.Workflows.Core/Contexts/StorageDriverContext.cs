using Grove.Workflows.Memory;

namespace Grove.Workflows;

/// <summary>
/// Provides context for storage drivers.
/// </summary>
public record StorageDriverContext(IExecutionContext ExecutionContext, Variable Variable, CancellationToken CancellationToken);