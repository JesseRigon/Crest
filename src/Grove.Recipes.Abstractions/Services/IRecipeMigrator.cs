using Grove.Data.Migration;

namespace Grove.Recipes.Services;

public interface IRecipeMigrator
{
    Task<string> ExecuteAsync(string recipeFileName, IDataMigration migration);
}
