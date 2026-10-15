using System.Collections.Generic;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Components.Primitives.GroveUpload.Change" /> event that is being raised.
/// </summary>
public class UploadChangeEventArgs
{
    /// <summary>
    /// Gets a collection of the selected files.
    /// </summary>
    public IEnumerable<FileInfo>? Files { get; set; }
}

