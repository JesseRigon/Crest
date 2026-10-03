using Crest.Workflows.Attributes;
using Crest.Workflows.Runtime.Activities;

namespace Crest.Workflows.Runtime.Stimuli;

/// <summary>
/// Contains information created by <see cref="RunTask"/>.  
/// </summary>
public record RunTaskStimulus(string TaskId, [property: ExcludeFromHash]string TaskName);