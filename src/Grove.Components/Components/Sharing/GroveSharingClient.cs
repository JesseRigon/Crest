using System.Net.Http.Json;
using System.Text.Json;

namespace Grove.Components.Sharing;

// The sharing API (docs/sharing.md § 8) as the Share panel, the Rules screens, the access
// indicators, the principal picker and the user editor use it. Every call runs as the signed-in
// caller: the server decides who may see or change an object's sharing, and what they may not
// see answers 404. Objects are named by kind and id; the tenant is kind "tenant" with no id.

/// <summary>Principal kinds, numbered as the server's (most general first).</summary>
public enum GrovePrincipalKind { Anyone = 0, Tenant = 1, Organization = 2, Role = 3, User = 4 }

/// <summary>Access levels, numbered as the server's; each includes the ones before it.</summary>
public enum GroveAccessLevel { None = 0, Viewer = 1, Commenter = 2, Editor = 3, Manager = 4, Admin = 5 }

public enum GroveShareIndicator { Private = 0, Shared = 1, Public = 2 }

public enum GroveConditionOperator { Equals = 0, In = 1, NotIn = 2 }

public enum GroveRoleKind { Assigned = 0, Derived = 1 }

public sealed record GrovePrincipal(GrovePrincipalKind Kind, string? Id = null, string? OrganizationId = null)
{
    public static readonly GrovePrincipal Anyone = new(GrovePrincipalKind.Anyone);

    public string Key => $"{Kind}:{Id}@{OrganizationId}";
}

public sealed record GroveShareEntry(GrovePrincipal Principal, string Name, GroveAccessLevel Level, bool IsBlock, DateTimeOffset? Starts, DateTimeOffset? Ends);

public sealed record GroveShareAccess(GrovePrincipal Principal, string Name, GroveAccessLevel Level, bool IsBlock, string DefinedOnKind, string DefinedOnId, int Depth, string? RuleId, string? Source, bool Wins);

public sealed record GroveSharePanelModel(
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
    List<GroveShareEntry> Entries,
    List<GroveShareAccess> Access);

public sealed class GroveShareCondition
{
    public string Fact { get; set; } = "extension";

    public GroveConditionOperator Operator { get; set; }

    public List<string> Values { get; set; } = [];
}

public sealed class GroveShareEffect
{
    public GrovePrincipal Principal { get; set; } = GrovePrincipal.Anyone;

    public string Name { get; set; } = string.Empty;

    public GroveAccessLevel Level { get; set; } = GroveAccessLevel.Viewer;

    public bool IsBlock { get; set; }

    public DateTimeOffset? Starts { get; set; }

    public DateTimeOffset? Ends { get; set; }
}

public sealed class GroveShareRule
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public List<GroveShareCondition> Conditions { get; set; } = [];

    public List<GroveShareEffect> Effects { get; set; } = [];

    public DateTimeOffset? Starts { get; set; }

    public DateTimeOffset? Ends { get; set; }
}

public sealed record GroveShareRuleLevel(string Kind, string Id, string Name, bool Editable, List<GroveShareRule> Rules);

public sealed record GroveShareIndicatorItem(string Kind, string Id, GroveShareIndicator Indicator);

public sealed record GrovePrincipalOption(GrovePrincipal Principal, string DisplayName, string? Description, bool IsDerived);

public sealed record GroveRegisteredRole(string RoleName, GroveRoleKind Kind, string? Provider, string? Description);

public sealed record GroveDerivedRole(string RoleName, string Relationship, string? OrganizationId, string? RelationshipId);

public sealed record GroveShareComment(string Id, string AuthorId, string? AuthorName, string Text, DateTimeOffset CreatedUtc, string? ReplyTo);

/// <summary>The outcome of a call: the value, or the server's reason when it refused.</summary>
public sealed record GroveSharingResult<T>(T? Value, string? Error, int Status)
{
    public bool Succeeded => Error is null;
}

public sealed class GroveSharingClient(HttpClient http)
{
    private const string Base = "api/sharing";

    public const string TenantKind = "tenant";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Query(string kind, string? id) => $"kind={Uri.EscapeDataString(kind)}&id={Uri.EscapeDataString(id ?? string.Empty)}";

    public Task<GroveSharingResult<GroveSharePanelModel>> GetPanelAsync(string kind, string? id) =>
        GetAsync<GroveSharePanelModel>($"{Base}/panel?{Query(kind, id)}");

    public Task<GroveSharingResult<bool>> SetEntryAsync(string kind, string? id, GrovePrincipal principal, GroveAccessLevel level, bool isBlock, DateTimeOffset? starts, DateTimeOffset? ends) =>
        SendAsync<bool>(HttpMethod.Put, $"{Base}/entries?{Query(kind, id)}", new { principal, level, isBlock, starts, ends });

    public Task<GroveSharingResult<bool>> RemoveEntryAsync(string kind, string? id, GrovePrincipal principal) =>
        SendAsync<bool>(HttpMethod.Post, $"{Base}/entries/remove?{Query(kind, id)}", principal);

    public Task<GroveSharingResult<List<GroveShareRuleLevel>>> GetRulesAsync(string kind, string? id) =>
        GetAsync<List<GroveShareRuleLevel>>($"{Base}/rules?{Query(kind, id)}");

    public Task<GroveSharingResult<bool>> SetRulesAsync(string kind, string? id, IReadOnlyList<GroveShareRule> rules) =>
        SendAsync<bool>(HttpMethod.Put, $"{Base}/rules?{Query(kind, id)}", rules);

    /// <summary>The indicators of the objects the caller may view; an object left out has none
    /// (sharing is off, or it was never calculated).</summary>
    public Task<GroveSharingResult<List<GroveShareIndicatorItem>>> GetIndicatorsAsync(IEnumerable<(string Kind, string Id)> targets) =>
        SendAsync<List<GroveShareIndicatorItem>>(HttpMethod.Post, $"{Base}/indicators", targets.Select(target => new { kind = target.Kind, id = target.Id }).ToList());

    public Task<GroveSharingResult<List<GrovePrincipalOption>>> SearchPrincipalsAsync(string? text, int take = 20) =>
        GetAsync<List<GrovePrincipalOption>>($"{Base}/principals?q={Uri.EscapeDataString(text ?? string.Empty)}&take={take}");

    public Task<GroveSharingResult<List<GroveRegisteredRole>>> GetRolesAsync() =>
        GetAsync<List<GroveRegisteredRole>>($"{Base}/roles");

    public Task<GroveSharingResult<List<GroveDerivedRole>>> GetDerivedRolesAsync(string userId) =>
        GetAsync<List<GroveDerivedRole>>($"{Base}/roles/users/{Uri.EscapeDataString(userId)}");

    public Task<GroveSharingResult<List<GroveShareComment>>> GetCommentsAsync(string kind, string id) =>
        GetAsync<List<GroveShareComment>>($"{Base}/comments?{Query(kind, id)}");

    public Task<GroveSharingResult<GroveShareComment>> AddCommentAsync(string kind, string id, string text, string? replyTo = null) =>
        SendAsync<GroveShareComment>(HttpMethod.Post, $"{Base}/comments?{Query(kind, id)}", new { text, replyTo });

    public Task<GroveSharingResult<bool>> RemoveCommentAsync(string kind, string id, string commentId) =>
        SendAsync<bool>(HttpMethod.Delete, $"{Base}/comments/{Uri.EscapeDataString(commentId)}?{Query(kind, id)}", null);

    private async Task<GroveSharingResult<T>> GetAsync<T>(string uri)
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

    private async Task<GroveSharingResult<T>> SendAsync<T>(HttpMethod method, string uri, object? body)
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

    private static async Task<GroveSharingResult<T>> ReadAsync<T>(HttpResponseMessage response)
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
    public static string LevelName(GroveAccessLevel level) => level switch
    {
        GroveAccessLevel.Admin => "Admin",
        GroveAccessLevel.None => "None",
        _ => level.ToString(),
    };
}
