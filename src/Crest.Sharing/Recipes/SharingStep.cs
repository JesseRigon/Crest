using System.Text.Json.Nodes;
using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.Sharing.Services;

namespace Crest.Sharing.Recipes;

/// <summary>
/// The <c>Sharing</c> recipe step: replaces the entries on objects, a content type's included
/// (requirement 73: "a content type carries entries, set in its settings and in recipes"), and
/// recalculates what they reach. A recipe is the tenant's own setup, so no Drive Admin check
/// applies; requirement 84 still holds: a shipped recipe grants Anyone or Tenant only on purpose.
/// <code>
/// { "name": "Sharing", "Objects": [ { "Kind": "contenttype", "Id": "Article",
///   "Entries": [ { "Principal": "role:Customer", "Level": "Viewer" },
///                { "Principal": "role:MemberAdmin@acme", "Level": "Editor" },
///                { "Principal": "user:sam", "Block": true, "Ends": "2027-01-01T00:00:00Z" } ] } ] }
/// </code>
/// A principal is <c>anyone</c>, <c>tenant</c>, <c>organization:id</c>, <c>role:name</c>,
/// <c>role:name@organization</c> or <c>user:id</c>; an object of kind <c>tenant</c> with an empty
/// id is the tenant itself.
/// </summary>
public sealed class SharingStep(IShareStore store, ShareAccessIndexer index) : NamedRecipeStepHandler("Sharing")
{
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Objects"] is not JsonArray objects)
        {
            return;
        }

        foreach (var node in objects.OfType<JsonObject>())
        {
            var kind = node["Kind"]?.GetValue<string>();
            var id = node["Id"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(kind))
            {
                context.Errors.Add("A Sharing step object needs a Kind.");
                continue;
            }

            var target = kind == ShareableRef.TenantRoot.Kind ? ShareableRef.TenantRoot : new ShareableRef(kind, id);
            var entries = new List<ShareEntry>();
            foreach (var entry in (node["Entries"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (!TryParsePrincipal(entry["Principal"]?.GetValue<string>(), out var principal))
                {
                    context.Errors.Add($"'{entry["Principal"]}' is not a principal (Sharing step, {target}).");
                    continue;
                }

                var isBlock = entry["Block"]?.GetValue<bool>() ?? false;
                var level = Enum.TryParse<AccessLevel>(entry["Level"]?.GetValue<string>(), ignoreCase: true, out var parsed) ? parsed : AccessLevel.Viewer;
                entries.Add(new ShareEntry(
                    principal,
                    isBlock ? AccessLevel.None : principal.Kind == SharePrincipalKind.Anyone && level > AccessLevel.Viewer ? AccessLevel.Viewer : level,
                    isBlock,
                    Instant(entry["Starts"]),
                    Instant(entry["Ends"])));
            }

            var document = await store.GetAsync(target) ?? new ShareDocument { Kind = target.Kind, ObjectId = target.Id };
            document.Entries = entries;
            await store.SaveAsync(document);
            await index.ApplyAsync(target, narrows: true);
        }
    }

    /// <summary>Parses <c>kind[:id][@organization]</c>.</summary>
    public static bool TryParsePrincipal(string? text, out SharePrincipal principal)
    {
        principal = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var colon = text.IndexOf(':');
        var kindText = colon < 0 ? text : text[..colon];
        var id = colon < 0 ? null : text[(colon + 1)..];
        if (!Enum.TryParse<SharePrincipalKind>(kindText, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return false;
        }

        string? organization = null;
        if (kind == SharePrincipalKind.Role && id?.LastIndexOf('@') is int at and > 0)
        {
            organization = id[(at + 1)..];
            id = id[..at];
        }

        if (kind is SharePrincipalKind.Anyone or SharePrincipalKind.Tenant ? id is not null : string.IsNullOrEmpty(id))
        {
            return false;
        }

        principal = new SharePrincipal(kind, id, organization);
        return true;
    }

    private static DateTimeOffset? Instant(JsonNode? node) =>
        node is null ? null : DateTimeOffset.Parse(node.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
}
