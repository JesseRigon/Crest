using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Management.Metadata.Models;

public static class ContentPartFieldDefinitionExtensions
{
    public static bool IsNamedPart(this ContentPartFieldDefinition fieldDefinition)
        => fieldDefinition.PartDefinition.IsReusable() && fieldDefinition.ContentTypePartDefinition.Name != fieldDefinition.PartDefinition.Name;
}
