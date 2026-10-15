namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Components.Primitives.GroveUpload.Error" /> event that is being raised.
/// </summary>
public class UploadErrorEventArgs
{
    /// <summary>
    /// Gets a message telling what caused the error.
    /// </summary>
    public string? Message { get; set; }
}

