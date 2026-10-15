namespace Grove.Tests.Apis.Context;

/// <summary>
/// Setup recipes that ship with the platform and that the integration tests provision tenants from.
/// </summary>
public static class TestRecipes
{
    /// <summary>
    /// The test project's own setup recipe (<c>Apis/Recipes/blog.recipe.json</c>, harvested by
    /// <see cref="TestRecipeHarvester"/>): the Blog content model, the Search Lucene index and the
    /// RecentBlogPosts query, with Grove.Themes.Site as the site theme and no sample content.
    /// </summary>
    public const string Blog = "Blog";

    /// <summary>The blank site recipe (Grove.Themes.Site): content management only, no sample content.</summary>
    public const string Blank = "Blank";
}
