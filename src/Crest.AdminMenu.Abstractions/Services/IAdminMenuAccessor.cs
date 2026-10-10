namespace Crest.AdminMenu.Services;

public interface IAdminMenuAccessor
{
    Task<IEnumerable<Models.AdminMenu>> GetAdminMenusAsync();
}
