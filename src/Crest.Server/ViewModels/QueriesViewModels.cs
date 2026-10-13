using System.Text.Json.Nodes;
using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using Crest.Queries;
using QueriesPermissions = Crest.Queries.Permissions;

namespace Crest.ViewModels;

public sealed record QueryCatalog(CrestQuery[] Queries, string[] Sources);

public sealed record CrestQuery(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject Properties)
{
    public static CrestQuery From(Query query) => new(query.Name, query.Source, query.Schema, query.ReturnContentItems, query.Properties.DeepClone() as JsonObject ?? []);
}

public sealed record QueryWrite(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject? Properties);

public sealed record QueryNames(string[]? Names);
