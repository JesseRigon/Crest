namespace Grove.Contents.AdminMenu.Services;

public interface IAdminMenuAccessor
{
    Task<IEnumerable<Models.AdminMenu>> GetAdminMenusAsync();
}
