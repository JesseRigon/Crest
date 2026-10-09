using Crest.ContentManagement.Metadata.Settings;

namespace Crest.ContentManagement.Metadata.Models;

public static class ContentPartFieldDefinitionExtensions
{
    public static bool IsNamedPart(this ContentPartFieldDefinition fieldDefinition)
        => fieldDefinition.PartDefinition.IsReusable() && fieldDefinition.ContentTypePartDefinition.Name != fieldDefinition.PartDefinition.Name;
}
