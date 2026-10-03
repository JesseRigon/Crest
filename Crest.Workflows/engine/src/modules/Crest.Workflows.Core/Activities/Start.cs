using System.Runtime.CompilerServices;
using Crest.Workflows.Attributes;
using JetBrains.Annotations;

namespace Crest.Workflows.Activities;

/// <summary>
/// Marks the start of a flowchart.
/// </summary>
[Activity("Crest.Workflows", "Flow", "A milestone activity that marks the start of a flowchart.", Kind = ActivityKind.Action)]
[PublicAPI]
public class Start : CodeActivity, IStartNode
{
    /// <inheritdoc />
    public Start([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }
}