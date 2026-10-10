using Crest.Workflows.Attributes;

namespace Crest.Workflows.Activities;

/// <summary>
/// Iterate over a set of values.
/// </summary>
[Activity("Crest.Workflows", "Looping", "Iterate over a set of values.")]
public class ForEach : ForEach<object>
{
}