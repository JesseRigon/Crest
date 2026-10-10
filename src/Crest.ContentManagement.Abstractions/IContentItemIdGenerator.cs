namespace Crest.ContentManagement;

public interface IContentItemIdGenerator
{
    string GenerateUniqueId(ContentItem contentItem);
}
