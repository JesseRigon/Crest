#nullable enable
using Crest.Queries.Structured;

namespace Crest.Queries.Structured;

/// <summary>The saved definition of a structured query, kept on the <see cref="Query"/> entity.</summary>
public sealed class StructuredQueryMetadata
{
    public StructuredQuery Definition { get; set; } = new();
}
