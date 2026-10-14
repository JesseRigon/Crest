using Crest.Routing;

namespace Crest.Apis.GraphQL;

/// <summary>
/// GraphiQL: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("graphiql", "GraphiQL", "tools", "Crest.Apis.GraphQL", "Admin", "Index", [GraphQLPermissions.ExecuteGraphQL.Name]);
    }
}
