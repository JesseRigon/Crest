using System.Runtime.CompilerServices;
using Grove.Workflows.Attributes;
using JetBrains.Annotations;

namespace Grove.Workflows.Activities;

/// <summary>
/// Marks the start of a flowchart.
/// </summary>
[Activity("Grove.Workflows", "Flow", "A milestone activity that marks the start of a flowchart.", Kind = ActivityKind.Action)]
[PublicAPI]
public class Start : CodeActivity, IStartNode
{
    /// <inheritdoc />
    public Start([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }
}