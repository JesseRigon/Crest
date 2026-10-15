namespace Grove.ContentManagement;

public interface IContentItemIdGenerator
{
    string GenerateUniqueId(ContentItem contentItem);
}
