using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Components;

/// <summary>
/// Represents the error.
/// </summary>
public partial class Error : ComponentBase
{
    [Parameter] public Exception Context { get; set; } = null!;
}