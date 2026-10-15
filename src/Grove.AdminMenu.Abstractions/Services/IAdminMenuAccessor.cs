namespace Grove.AdminMenu.Services;

public interface IAdminMenuAccessor
{
    Task<IEnumerable<Models.AdminMenu>> GetAdminMenusAsync();
}
