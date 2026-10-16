using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Models;

public class ContentTypeDefinition : ContentDefinition
{
    public ContentTypeDefinition(string name, string displayName, IEnumerable<ContentTypeFieldSetDefinition> parts, JsonObject settings, IEnumerable<EmbeddedContentDefinition> embeddings = null)
    {
        Name = name;
        DisplayName = displayName;
        FieldSets = parts.ToList();
        Embeddings = embeddings?.ToList() ?? [];
        Settings = settings.Clone();

        foreach (var part in FieldSets)
        {
            part.ContentTypeDefinition = this;
        }

        foreach (var embedding in Embeddings)
        {
            embedding.ContentTypeDefinition = this;
        }
    }

    public ContentTypeDefinition(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
        FieldSets = [];
        Embeddings = [];
        Settings = [];
    }

    [Required, StringLength(1024)]
    public string DisplayName { get; private set; }

    public IEnumerable<ContentTypeFieldSetDefinition> FieldSets { get; private set; }

    /// <summary>The embeddings the type declares (docs/content-engine.md › Embedded content items).</summary>
    public IEnumerable<EmbeddedContentDefinition> Embeddings { get; private set; }

    /// <summary>
    /// Returns the <see cref="DisplayName"/> value of the type if defined,
    /// or the <see cref="ContentDefinition.Name"/> otherwise.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return string.IsNullOrEmpty(DisplayName)
            ? Name
            : DisplayName;
    }
}
