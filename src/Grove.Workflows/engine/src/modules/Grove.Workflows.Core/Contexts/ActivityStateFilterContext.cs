using System.Text.Json;
using Grove.Workflows.Models;

namespace Grove.Workflows;

public record ActivityStateFilterContext(ActivityExecutionContext ActivityExecutionContext, InputDescriptor InputDescriptor, JsonElement Value, CancellationToken CancellationToken);