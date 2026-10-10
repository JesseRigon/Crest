namespace Crest.AdminMenu;

/// <summary>
/// Raised by the admin menu service when a menu is saved (created, or any of its nodes added,
/// renamed, moved or removed) and when a menu is deleted, so that whatever hangs off a menu
/// (translations of its captions, placement, layout) follows it.
/// </summary>
public interface IAdminMenuEventHandler
{
    Task SavedAsync(Models.AdminMenu menu);

    Task RemovedAsync(Models.AdminMenu menu);
}
