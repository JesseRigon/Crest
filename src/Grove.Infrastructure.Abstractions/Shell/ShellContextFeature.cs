using Microsoft.AspNetCore.Http;
using Grove.Environment.Shell.Builders;

namespace Grove.Environment.Shell;

/// <summary>
/// Used to capture the shell context and original path infos.
/// </summary>
public class ShellContextFeature
{
    /// <summary>
    /// The current shell context.
    /// </summary>
    public ShellContext ShellContext { get; init; }

    /// <summary>
    /// The original path base.
    /// </summary>
    public PathString OriginalPathBase { get; init; }

    /// <summary>
    /// The original path.
    /// </summary>
    public PathString OriginalPath { get; init; }
}
