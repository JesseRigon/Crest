using System.Text.Json.Serialization;
using Crest.Data.Documents;
using Crest.Documents;

namespace Crest.Services;

// Vanilla Crest has no concept of "which navigation surface does this custom admin
// menu belong to" — Crest.AdminMenu's own AdminMenu document (see
// Crest.AdminMenu.Abstractions.Models.AdminMenu, a NuGet package type we can't edit)
// unconditionally injects every enabled custom menu into the "admin" sidebar tree. This is
// a Crest-owned companion document, keyed by that same AdminMenu.Id, tracking which
// placement each custom menu actually belongs to (default Admin for anything without an
// entry, so pre-existing menus are unaffected) and — for Local/User placements — an
// independent Enabled flag, since the underlying AdminMenu.Enabled gets forced false for
// those to keep Crest's own coordinator from also rendering them into the sidebar.
[JsonConverter(typeof(JsonStringEnumConverter<MenuPlacement>))]
public enum MenuPlacement
{
    Admin,
    Local,
    User,
}

public sealed class MenuPlacementEntry
{
    public MenuPlacement Placement { get; set; } = MenuPlacement.Admin;
    public bool Enabled { get; set; } = true;
}

public sealed class CrestMenuPlacementDocument : Document
{
    public Dictionary<string, MenuPlacementEntry> Entries { get; set; } = [];
}

public sealed class MenuPlacementService(IDocumentManager<CrestMenuPlacementDocument> documents)
{
    public static readonly MenuPlacementEntry DefaultEntry = new();

    public async ValueTask<MenuPlacementEntry> GetAsync(string menuId)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Entries.TryGetValue(menuId, out var entry) ? entry : DefaultEntry;
    }

    public async ValueTask<IReadOnlyDictionary<string, MenuPlacementEntry>> GetAllAsync()
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Entries;
    }

    public async ValueTask SetAsync(string menuId, MenuPlacement placement, bool enabled)
    {
        var document = await documents.GetOrCreateMutableAsync();
        document.Entries[menuId] = new MenuPlacementEntry { Placement = placement, Enabled = enabled };
        await documents.UpdateAsync(document);
    }

    public async ValueTask RemoveAsync(string menuId)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (document.Entries.Remove(menuId))
        {
            await documents.UpdateAsync(document);
        }
    }
}
