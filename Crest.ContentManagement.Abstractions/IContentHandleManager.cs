namespace Crest.ContentManagement;

public interface IContentHandleManager
{
    Task<string> GetContentItemIdAsync(string handle);
}
