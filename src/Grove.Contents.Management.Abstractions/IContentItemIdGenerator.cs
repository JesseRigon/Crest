namespace Grove.Contents.Management;

public interface IContentItemIdGenerator
{
    string GenerateUniqueId(ContentItem contentItem);
}
