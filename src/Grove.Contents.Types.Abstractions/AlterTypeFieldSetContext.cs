using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Types;

/// <summary>
/// Data transfer object for altering a content type part.
/// </summary>
public class AlterTypeFieldSetContext
{
    /// <summary>
    /// Gets or sets the name of the content type.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the field set definition.
    /// </summary>
    public ContentFieldSetDefinition FieldSetDefinition { get; set; }

    /// <summary>
    /// Gets or sets the name of the part instance on the type.
    /// </summary>
    public string FieldSetName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the part.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of the part.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the editor for the part.
    /// </summary>
    public string Editor { get; set; }

    /// <summary>
    /// Gets or sets the display mode for the part.
    /// </summary>
    public string DisplayMode { get; set; }
}
