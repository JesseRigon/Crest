namespace Grove.Contents.Management;

public interface IContentHandleProvider
{
    int Order { get; }
    Task<string> GetContentItemIdAsync(string handle);
}
