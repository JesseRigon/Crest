namespace Crest.ContentManagement;

public interface IContentHandleProvider
{
    int Order { get; }
    Task<string> GetContentItemIdAsync(string handle);
}
