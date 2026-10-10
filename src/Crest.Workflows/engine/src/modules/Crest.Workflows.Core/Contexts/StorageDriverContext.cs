using Crest.Workflows.Memory;

namespace Crest.Workflows;

/// <summary>
/// Provides context for storage drivers.
/// </summary>
public record StorageDriverContext(IExecutionContext ExecutionContext, Variable Variable, CancellationToken CancellationToken);