using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Models;

public class ContentTypeFieldSetDefinition : ContentDefinition
{
    public ContentTypeFieldSetDefinition(string name, ContentFieldSetDefinition contentFieldSetDefinition, JsonObject settings)
    {
        Name = name;
        FieldSetDefinition = contentFieldSetDefinition;
        Settings = settings;

        foreach (var field in FieldSetDefinition.Fields)
        {
            field.ContentTypeFieldSetDefinition = this;
        }
    }

    public ContentFieldSetDefinition FieldSetDefinition { get; private set; }
    public ContentTypeDefinition ContentTypeDefinition { get; set; }
}
