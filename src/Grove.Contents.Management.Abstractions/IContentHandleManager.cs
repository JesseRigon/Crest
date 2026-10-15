namespace Grove.Contents.Management;

public interface IContentHandleManager
{
    Task<string> GetContentItemIdAsync(string handle);
}
