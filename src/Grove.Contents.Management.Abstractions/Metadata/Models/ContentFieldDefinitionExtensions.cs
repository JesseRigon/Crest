using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Management.Metadata.Models;

public static class ContentFieldDefinitionExtensions
{
    public static bool IsNamedFieldSet(this ContentFieldDefinition fieldDefinition)
        => fieldDefinition.FieldSetDefinition.IsReusable() && fieldDefinition.ContentTypeFieldSetDefinition.Name != fieldDefinition.FieldSetDefinition.Name;
}
