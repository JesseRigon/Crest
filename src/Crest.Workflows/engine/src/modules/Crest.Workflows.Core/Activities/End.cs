using System.Runtime.CompilerServices;
using Crest.Workflows.Attributes;
using JetBrains.Annotations;

namespace Crest.Workflows.Activities;

/// <summary>
/// Marks the end of a flowchart, causing the flowchart to complete.
/// </summary>
[Activity("Crest.Workflows", "Flow", "A milestone activity that marks the end of a flowchart.", Kind = ActivityKind.Action)]
[PublicAPI]
public class End : CodeActivity, ITerminalNode
{
    /// <inheritdoc />
    public End([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }
}