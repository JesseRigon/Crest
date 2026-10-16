namespace Grove.Contents.Management.Metadata.Models;

public class ContentFieldTypeDefinition
{
    public ContentFieldTypeDefinition(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }
}
