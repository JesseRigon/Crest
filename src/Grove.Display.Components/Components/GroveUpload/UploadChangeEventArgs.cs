using System.Collections.Generic;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Display.Components.Primitives.GroveUpload.Change" /> event that is being raised.
/// </summary>
public class UploadChangeEventArgs
{
    /// <summary>
    /// Gets a collection of the selected files.
    /// </summary>
    public IEnumerable<FileInfo>? Files { get; set; }
}

