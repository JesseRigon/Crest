using System.Net.Http.Json;
using System.Text.Json;

namespace Crest.Components.Sharing;

// The sharing API (docs/sharing.md § 8) as the Share panel, the Rules screens, the access
// indicators, the principal picker and the user editor use it. Every call runs as the signed-in
// caller: the server decides who may see or change an object's sharing, and what they may not
// see answers 404. Objects are named by kind and id; the tenant is kind "tenant" with no id.

/// <summary>Principal kinds, numbered as the server's (most general first).</summary>
public enum CrestPrincipalKind { Anyone = 0, Tenant = 1, Organization = 2, Role = 3, User = 4 }

/// <summary>Access levels, numbered as the server's; each includes the ones before it.</summary>
public enum CrestAccessLevel { None = 0, Viewer = 1, Commenter = 2, Editor = 3, Manager = 4, Admin = 5 }

public enum CrestShareIndicator { Private = 0, Shared = 1, Public = 2 }

public enum CrestConditionOperator { Equals = 0, In = 1, NotIn = 2 }

public enum CrestRoleKind { Assigned = 0, Derived = 1 }

public sealed record CrestPrincipal(CrestPrincipalKind Kind, string? Id = null, string? OrganizationId = null)
{
    public static readonly CrestPrincipal Anyone = new(CrestPrincipalKind.Anyone);

    public string Key => $"{Kind}:{Id}@{OrganizationId}";
}

public sealed record CrestShareEntry(CrestPrincipal Principal, string Name, CrestAccessLevel Level, bool IsBlock, DateTimeOffset? Starts, DateTimeOffset? Ends);

public sealed record CrestShareAccess(CrestPrincipal Principal, string Name, CrestAccessLevel Level, bool IsBlock, string DefinedOnKind, string DefinedOnId, int Depth, string? RuleId, string? Source, bool Wins);

public sealed record CrestSharePanelModel(
    string Kind,
    string Id,
    string Name,
    bool IsTenant,
    bool IsRoot,
    bool IsAdmin,
    bool IsPublic,
    bool Pending,
    string? PublicLink,
    bool CanShareWithAnyone,
    List<CrestShareEntry> Entries,
    List<CrestShareAccess> Access);

public sealed class CrestShareCondition
{
    public string Fact { get; set; } = "extension";

    public CrestConditionOperator Operator { get; set; }

    public List<string> Values { get; set; } = [];
}

public sealed class CrestShareEffect
{
    public CrestPrincipal Principal { get; set; } = CrestPrincipal.Anyone;

    public string Name { get; set; } = string.Empty;

    public CrestAccessLevel Level { get; set; } = CrestAccessLevel.Viewer;

    public bool IsBlock { get; set; }

    public DateTimeOffset? Starts { get; set; }

    public DateTimeOffset? Ends { get; set; }
}

public sealed class CrestShareRule
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public List<CrestShareCondition> Conditions { get; set; } = [];

    public List<CrestShareEffect> Effects { get; set; } = [];

    public DateTimeOffset? Starts { get; set; }

    public DateTimeOffset? Ends { get; set; }
}

public sealed record CrestShareRuleLevel(string Kind, string Id, string Name, bool Editable, List<CrestShareRule> Rules);

public sealed record CrestShareIndicatorItem(string Kind, string Id, CrestShareIndicator Indicator);

public sealed record CrestPrincipalOption(CrestPrincipal Principal, string DisplayName, string? Description, bool IsDerived);

public sealed record CrestRegisteredRole(string RoleName, CrestRoleKind Kind, string? Provider, string? Description);

public sealed record CrestDerivedRole(string RoleName, string Relationship, string? OrganizationId, string? RelationshipId);

public sealed record CrestShareComment(string Id, string AuthorId, string? AuthorName, string Text, DateTimeOffset CreatedUtc, string? ReplyTo);

/// <summary>The outcome of a call: the value, or the server's reason when it refused.</summary>
public sealed record CrestSharingResult<T>(T? Value, string? Error, int Status)
{
    public bool Succeeded => Error is null;
}

public sealed class CrestSharingClient(HttpClient http)
{
    private const string Base = "api/sharing";

    public const string TenantKind = "tenant";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Query(string kind, string? id) => $"kind={Uri.EscapeDataString(kind)}&id={Uri.EscapeDataString(id ?? string.Empty)}";

    public Task<CrestSharingResult<CrestSharePanelModel>> GetPanelAsync(string kind, string? id) =>
        GetAsync<CrestSharePanelModel>($"{Base}/panel?{Query(kind, id)}");

    public Task<CrestSharingResult<bool>> SetEntryAsync(string kind, string? id, CrestPrincipal principal, CrestAccessLevel level, bool isBlock, DateTimeOffset? starts, DateTimeOffset? ends) =>
        SendAsync<bool>(HttpMethod.Put, $"{Base}/entries?{Query(kind, id)}", new { principal, level, isBlock, starts, ends });

    public Task<CrestSharingResult<bool>> RemoveEntryAsync(string kind, string? id, CrestPrincipal principal) =>
        SendAsync<bool>(HttpMethod.Post, $"{Base}/entries/remove?{Query(kind, id)}", principal);

    public Task<CrestSharingResult<List<CrestShareRuleLevel>>> GetRulesAsync(string kind, string? id) =>
        GetAsync<List<CrestShareRuleLevel>>($"{Base}/rules?{Query(kind, id)}");

    public Task<CrestSharingResult<bool>> SetRulesAsync(string kind, string? id, IReadOnlyList<CrestShareRule> rules) =>
        SendAsync<bool>(HttpMethod.Put, $"{Base}/rules?{Query(kind, id)}", rules);

    /// <summary>The indicators of the objects the caller may view; an object left out has none
    /// (sharing is off, or it was never calculated).</summary>
    public Task<CrestSharingResult<List<CrestShareIndicatorItem>>> GetIndicatorsAsync(IEnumerable<(string Kind, string Id)> targets) =>
        SendAsync<List<CrestShareIndicatorItem>>(HttpMethod.Post, $"{Base}/indicators", targets.Select(target => new { kind = target.Kind, id = target.Id }).ToList());

    public Task<CrestSharingResult<List<CrestPrincipalOption>>> SearchPrincipalsAsync(string? text, int take = 20) =>
        GetAsync<List<CrestPrincipalOption>>($"{Base}/principals?q={Uri.EscapeDataString(text ?? string.Empty)}&take={take}");

    public Task<CrestSharingResult<List<CrestRegisteredRole>>> GetRolesAsync() =>
        GetAsync<List<CrestRegisteredRole>>($"{Base}/roles");

    public Task<CrestSharingResult<List<CrestDerivedRole>>> GetDerivedRolesAsync(string userId) =>
        GetAsync<List<CrestDerivedRole>>($"{Base}/roles/users/{Uri.EscapeDataString(userId)}");

    public Task<CrestSharingResult<List<CrestShareComment>>> GetCommentsAsync(string kind, string id) =>
        GetAsync<List<CrestShareComment>>($"{Base}/comments?{Query(kind, id)}");

    public Task<CrestSharingResult<CrestShareComment>> AddCommentAsync(string kind, string id, string text, string? replyTo = null) =>
        SendAsync<CrestShareComment>(HttpMethod.Post, $"{Base}/comments?{Query(kind, id)}", new { text, replyTo });

    public Task<CrestSharingResult<bool>> RemoveCommentAsync(string kind, string id, string commentId) =>
        SendAsync<bool>(HttpMethod.Delete, $"{Base}/comments/{Uri.EscapeDataString(commentId)}?{Query(kind, id)}", null);

    private async Task<CrestSharingResult<T>> GetAsync<T>(string uri)
    {
        try
        {
            using var response = await http.GetAsync(uri);
            return await ReadAsync<T>(response);
        }
        catch (HttpRequestException exception)
        {
            return new(default, exception.Message, 0);
        }
    }

    private async Task<CrestSharingResult<T>> SendAsync<T>(HttpMethod method, string uri, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            using var response = await http.SendAsync(request);
            return await ReadAsync<T>(response);
        }
        catch (HttpRequestException exception)
        {
            return new(default, exception.Message, 0);
        }
    }

    private static async Task<CrestSharingResult<T>> ReadAsync<T>(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            return new(default, string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase ?? $"HTTP {status}" : text.Trim('"'), status);
        }

        if (typeof(T) == typeof(bool))
        {
            return new((T)(object)true, null, status);
        }

        return new(await response.Content.ReadFromJsonAsync<T>(Json), null, status);
    }

    /// <summary>How a level reads in the interface.</summary>
    public static string LevelName(CrestAccessLevel level) => level switch
    {
        CrestAccessLevel.Admin => "Admin",
        CrestAccessLevel.None => "None",
        _ => level.ToString(),
    };
}
