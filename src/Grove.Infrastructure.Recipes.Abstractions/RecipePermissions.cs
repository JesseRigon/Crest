using Grove.Security.Permissions;

namespace Grove.Infrastructure.Recipes;

public static class RecipePermissions
{
    public static readonly Permission ManageRecipes = new Permission("ManageRecipes", "Manage Recipes", isSecurityCritical: true);
}
