namespace Crest.Tests.Apis.Context;

/// <summary>
/// Setup recipes that ship with the platform and that the integration tests provision tenants from.
/// </summary>
public static class TestRecipes
{
    /// <summary>
    /// The test project's own setup recipe (<c>Apis/Recipes/blog.recipe.json</c>, harvested by
    /// <see cref="TestRecipeHarvester"/>): the Blog content model, the Search Lucene index and the
    /// RecentBlogPosts query, with SafeMode as the site theme and no sample theme.
    /// </summary>
    public const string Blog = "Blog";

    /// <summary>The blank site recipe from the admin theme: content management only, no sample content.</summary>
    public const string Blank = "Blank";
}
