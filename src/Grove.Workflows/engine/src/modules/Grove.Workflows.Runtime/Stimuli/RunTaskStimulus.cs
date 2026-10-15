using Grove.Workflows.Attributes;
using Grove.Workflows.Runtime.Activities;

namespace Grove.Workflows.Runtime.Stimuli;

/// <summary>
/// Contains information created by <see cref="RunTask"/>.  
/// </summary>
public record RunTaskStimulus(string TaskId, [property: ExcludeFromHash]string TaskName);