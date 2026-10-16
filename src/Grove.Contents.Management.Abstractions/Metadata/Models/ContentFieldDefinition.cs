using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Models;

public class ContentFieldDefinition : ContentDefinition
{
    public ContentFieldDefinition(ContentFieldTypeDefinition fieldTypeDefinition, string name, JsonObject settings)
    {
        Name = name;
        FieldTypeDefinition = fieldTypeDefinition;
        Settings = settings;
    }

    public ContentFieldTypeDefinition FieldTypeDefinition { get; private set; }
    public ContentFieldSetDefinition FieldSetDefinition { get; set; }
    public ContentTypeFieldSetDefinition ContentTypeFieldSetDefinition { get; set; }
}
