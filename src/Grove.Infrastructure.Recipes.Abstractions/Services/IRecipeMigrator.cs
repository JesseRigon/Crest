using Grove.Data.Migration;

namespace Grove.Infrastructure.Recipes.Services;

public interface IRecipeMigrator
{
    Task<string> ExecuteAsync(string recipeFileName, IDataMigration migration);
}
