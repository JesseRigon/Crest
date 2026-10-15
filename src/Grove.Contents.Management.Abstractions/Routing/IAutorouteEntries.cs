namespace Grove.Contents.Management.Routing;

public interface IAutorouteEntries
{
    Task<(bool, AutorouteEntry)> TryGetEntryByPathAsync(string path);
    Task<(bool, AutorouteEntry)> TryGetEntryByContentItemIdAsync(string contentItemId);
    Task UpdateEntriesAsync();
}
