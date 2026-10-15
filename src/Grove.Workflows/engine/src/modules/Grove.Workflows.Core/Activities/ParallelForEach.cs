using System.Runtime.CompilerServices;
using Grove.Workflows.Attributes;

namespace Grove.Workflows.Activities;

/// <summary>
/// Schedule an activity for each item in parallel.
/// </summary>
[Activity("Grove.Workflows", "Looping", "Schedule an activity for each item in parallel.")]
public class ParallelForEach : ParallelForEach<object>
{
    /// <inheritdoc />
    public ParallelForEach([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }
}