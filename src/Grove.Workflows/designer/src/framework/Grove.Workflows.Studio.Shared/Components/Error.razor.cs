using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.Components;

/// <summary>
/// Represents the error.
/// </summary>
public partial class Error : ComponentBase
{
    [Parameter] public Exception Context { get; set; } = null!;
}