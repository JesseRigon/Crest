using Microsoft.AspNetCore.Components.Forms;

namespace Grove.Components.Primitives;

/// <summary>
/// Represents the preview which <see cref="Grove.Components.Primitives.GroveUpload" /> displays for selected files.
/// </summary>
public class PreviewFileInfo : FileInfo
{
    /// <summary>
    /// Initializes a new instance of PreviewFileInfo from a browser file.
    /// </summary>
    /// <param name="files">The browser file.</param>
    public PreviewFileInfo(IBrowserFile files) : base(files)
    {
    }

    /// <summary>
    /// Initializes a new, empty instance of PreviewFileInfo.
    /// </summary>
    public PreviewFileInfo()
    {
    }

    /// <summary>
    /// Gets the URL of the previewed file.
    /// </summary>
    public string? Url { get; set; }
}

