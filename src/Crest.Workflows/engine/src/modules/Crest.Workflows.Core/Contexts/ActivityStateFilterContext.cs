using System.Text.Json;
using Crest.Workflows.Models;

namespace Crest.Workflows;

public record ActivityStateFilterContext(ActivityExecutionContext ActivityExecutionContext, InputDescriptor InputDescriptor, JsonElement Value, CancellationToken CancellationToken);