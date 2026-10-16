using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Models;

public class ContentFieldSetDefinition : ContentDefinition
{
    public ContentFieldSetDefinition(string name)
    {
        Name = name;
        Fields = [];
        Settings = [];
    }

    public ContentFieldSetDefinition(string name, IEnumerable<ContentFieldDefinition> fields, JsonObject settings)
    {
        Name = name;
        Fields = fields.ToList();
        Settings = settings.Clone();

        foreach (var field in Fields)
        {
            field.FieldSetDefinition = this;
        }
    }

    public IEnumerable<ContentFieldDefinition> Fields { get; private set; }
}
