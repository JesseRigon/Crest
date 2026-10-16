using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Records;

/// <summary>
/// Represents a part and its settings in a type.
/// </summary>
public class ContentTypeFieldSetDefinitionRecord
{
    /// <summary>
    /// Gets or sets the part name.
    /// </summary>
    public string FieldSetName { get; set; }

    /// <summary>
    /// Gets or sets the name of the part.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the settings of the part for this type.
    /// </summary>
    public JsonObject Settings { get; set; }
}
