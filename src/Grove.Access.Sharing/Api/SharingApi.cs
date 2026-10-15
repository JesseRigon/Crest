using Grove.Access;
using Grove.Access.Roles;
using Grove.Access.Sharing.Services;
using Grove.Access.Users;
using Grove.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Access.Sharing.Api;

/// <summary>
/// Sharing over HTTP for the interface (docs/sharing.md § 8): the Share panel (96), the Rules
/// screens (97), access indicators (98), the principal picker (46), the role registry and a
/// user's derived roles (47), and comments (71). Every call runs as the gated caller through the
/// sharing services, which decide: an object's sharing is its tree's Admins' to see and change,
/// the tenant's needs <c>ManageTenantSharing</c>. An object the caller cannot view answers 404.
/// Objects are named by <c>kind</c> and <c>id</c> in the query; the tenant is kind <c>tenant</c>.
/// </summary>
public sealed class SharingApi : ApiContributor
{
    public override string Name => "Sharing";

    public override string FeatureId => "Grove.Access.Sharing";

    public override string RoutePrefix => "api/sharing";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("").RequireAuthorization();
        routes.MapGet("panel", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id) => ApiHandlers.For<SharingHandlers>(http).PanelAsync(kind, id));
        routes.MapPut("entries", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id, [FromBody] ShareEntryRequest request) => ApiHandlers.For<SharingHandlers>(http).SetEntryAsync(kind, id, request));
        routes.MapPost("entries/remove", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id, [FromBody] SharePrincipalDto principal) => ApiHandlers.For<SharingHandlers>(http).RemoveEntryAsync(kind, id, principal));
        routes.MapGet("rules", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id) => ApiHandlers.For<SharingHandlers>(http).RulesAsync(kind, id));
        routes.MapPut("rules", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id, [FromBody] List<ShareRuleDto> rules) => ApiHandlers.For<SharingHandlers>(http).SetRulesAsync(kind, id, rules));
        routes.MapPost("indicators", (HttpContext http, [FromBody] List<ShareObjectDto> targets) => ApiHandlers.For<SharingHandlers>(http).IndicatorsAsync(targets));
        routes.MapGet("principals", (HttpContext http, [FromQuery] string? q, [FromQuery] int? take) => ApiHandlers.For<SharingHandlers>(http).PrincipalsAsync(q, take ?? 20));
        routes.MapGet("roles", ApiHandlers.Handle<SharingHandlers>(h => h.RolesAsync()));
        routes.MapGet("roles/users/{userId}", (HttpContext http, [FromRoute] string userId) => ApiHandlers.For<SharingHandlers>(http).DerivedRolesAsync(userId));
        routes.MapGet("comments", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id) => ApiHandlers.For<SharingHandlers>(http).CommentsAsync(kind, id));
        routes.MapPost("comments", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id, [FromBody] ShareCommentRequest request) => ApiHandlers.For<SharingHandlers>(http).AddCommentAsync(kind, id, request));
        routes.MapDelete("comments/{commentId}", (HttpContext http, [FromQuery] string kind, [FromQuery] string? id, [FromRoute] string commentId) => ApiHandlers.For<SharingHandlers>(http).RemoveCommentAsync(kind, id, commentId));
    }
}

public sealed class SharingHandlers(
    ISharingService sharing,
    SharingResolver resolver,
    IShareStore store,
    ShareableKinds kinds,
    ShareAccessIndexer index,
    IShareIndicators indicators,
    IShareComments comments,
    IEnumerable<ISharePrincipalDirectory> directories,
    IRoleRegistry roles,
    IAccessDecision decision,
    ICallerContextAccessor callers) : ApiHandlers
{
    private const int MaxPrincipals = 50;

    /// <summary>The Share panel: the object's entries, who has access and why, the public link
    /// and whether a change is still being applied (requirement 80).</summary>
    public Task<IResult> PanelAsync(string kind, string? id) => RunAsync(async () =>
    {
        var target = Target(kind, id);
        var cancellationToken = HttpContext.RequestAborted;
        var entries = await sharing.GetEntriesAsync(target, cancellationToken);

        IReadOnlyList<ShareDecision> decisions = [];
        var isAdmin = true;
        if (!target.IsTenant)
        {
            try
            {
                decisions = await sharing.ExplainAsync(target, cancellationToken);
            }
            catch (AccessDeniedException)
            {
                // A holder of the kind's admin-assignment permission sees the Admin grants they
                // manage and nothing else of how the object is shared.
                isAdmin = false;
            }
        }

        var item = target.IsTenant ? null : kinds.Find(target.Kind) is { } shareable ? await shareable.GetAsync(target.Id, cancellationToken) : null;
        var resolution = target.IsTenant || !isAdmin || callers.Current is not { } caller ? null : await resolver.ResolveAsync(caller, target, cancellationToken);
        var names = await NamesAsync([.. entries.Select(entry => entry.Principal), .. decisions.Select(decision => decision.Principal)]);

        var winners = decisions
            .GroupBy(decision => decision.Principal)
            .Select(group => SharingResolver.DecideFor(group))
            .OfType<ShareDecision>()
            .ToHashSet();

        var pending = !target.IsTenant && isAdmin && (await index.VerifyAsync(target, cancellationToken)).Count > 0;
        var link = resolution is { IsPublic: true } && kinds.Find(target.Kind) is IShareableLinks links ? await links.LinkAsync(target.Id, cancellationToken) : null;

        return Ok(new SharePanelDto(
            target.Kind,
            target.Id,
            item?.DisplayName ?? (target.IsTenant ? "Everything on this site" : target.Id),
            target.IsTenant,
            item?.IsRoot ?? false,
            isAdmin,
            resolution?.IsPublic ?? false,
            pending,
            link,
            await AllowsAsync(SharingPermissionNames.ShareWithAnyone),
            [.. entries.Select(entry => new ShareEntryDto(Principal(entry.Principal), Name(names, entry.Principal), entry.Level, entry.IsBlock, entry.Starts, entry.Ends))],
            [.. decisions.Select(decision => new ShareAccessDto(
                Principal(decision.Principal),
                Name(names, decision.Principal),
                decision.Effect.Level,
                decision.Effect.IsBlock,
                decision.DefinedOn.Kind,
                decision.DefinedOn.Id,
                decision.Depth,
                decision.RuleId,
                decision.Source,
                winners.Contains(decision)))]));
    });

    /// <summary>Adds or replaces the entry for its principal.</summary>
    public Task<IResult> SetEntryAsync(string kind, string? id, ShareEntryRequest request) => RunAsync(async () =>
    {
        var entry = new ShareEntry(Principal(request.Principal), request.IsBlock ? AccessLevel.None : request.Level, request.IsBlock, request.Starts, request.Ends);
        await sharing.SetEntryAsync(Target(kind, id), entry, HttpContext.RequestAborted);
        return NoContent();
    });

    public Task<IResult> RemoveEntryAsync(string kind, string? id, SharePrincipalDto principal) => RunAsync(async () =>
    {
        await sharing.RemoveEntryAsync(Target(kind, id), Principal(principal), HttpContext.RequestAborted);
        return NoContent();
    });

    /// <summary>
    /// The Rules screen (requirements 67, 97): the rules from the tenant down to the object, each
    /// level with the rules defined there in run order; only the object's own level is editable
    /// here. Rules below the object are not shown.
    /// </summary>
    public Task<IResult> RulesAsync(string kind, string? id) => RunAsync(async () =>
    {
        var target = Target(kind, id);
        var cancellationToken = HttpContext.RequestAborted;
        var own = await sharing.GetRulesAsync(target, cancellationToken);
        var levels = new List<ShareRuleLevelDto>();
        if (!target.IsTenant)
        {
            levels.Add(new ShareRuleLevelDto(ShareableRef.TenantRoot.Kind, string.Empty, "Everything on this site", false, [.. ((await store.GetAsync(ShareableRef.TenantRoot))?.Rules ?? []).Select(Rule)]));
            var chain = await resolver.ChainAsync(target, cancellationToken) ?? [];
            foreach (var above in chain.Take(Math.Max(0, chain.Count - 1)))
            {
                levels.Add(new ShareRuleLevelDto(above.Ref.Kind, above.Ref.Id, above.DisplayName ?? above.Ref.Id, false, [.. ((await store.GetAsync(above.Ref))?.Rules ?? []).Select(Rule)]));
            }
        }

        var item = target.IsTenant ? null : kinds.Find(target.Kind) is { } shareable ? await shareable.GetAsync(target.Id, cancellationToken) : null;
        levels.Add(new ShareRuleLevelDto(target.Kind, target.Id, item?.DisplayName ?? (target.IsTenant ? "Everything on this site" : target.Id), true, [.. own.Select(Rule)]));
        return Ok(levels);
    });

    /// <summary>Replaces the object's rules, in run order.</summary>
    public Task<IResult> SetRulesAsync(string kind, string? id, List<ShareRuleDto> rules) => RunAsync(async () =>
    {
        await sharing.SetRulesAsync(Target(kind, id), [.. rules.Select(Rule)], HttpContext.RequestAborted);
        return NoContent();
    });

    /// <summary>The access indicators of the objects the caller may view (requirement 98).</summary>
    public Task<IResult> IndicatorsAsync(List<ShareObjectDto> targets) => RunAsync(async () =>
    {
        var found = await indicators.GetAsync([.. targets.Select(target => new ShareableRef(target.Kind, target.Id))], HttpContext.RequestAborted);
        return Ok(found.Select(pair => new ShareIndicatorDto(pair.Key.Kind, pair.Key.Id, pair.Value)).ToList());
    });

    /// <summary>The picker (requirement 46): principals of every kind, derived roles labelled.</summary>
    public Task<IResult> PrincipalsAsync(string? q, int take = 20) => RunAsync(async () =>
    {
        take = Math.Clamp(take, 1, MaxPrincipals);
        var options = new List<SharePrincipalOptionDto>();
        foreach (var directory in directories)
        {
            options.AddRange((await directory.SearchAsync(q, take, HttpContext.RequestAborted))
                .Select(option => new SharePrincipalOptionDto(Principal(option.Principal), option.DisplayName, option.Description, option.IsDerived)));
        }

        return Ok(options.DistinctBy(option => (option.Principal.Kind, option.Principal.Id, option.Principal.OrganizationId)).Take(take).ToList());
    });

    /// <summary>The role registry: every role with its kind (requirement 44).</summary>
    public Task<IResult> RolesAsync() => RunAsync(async () =>
        Ok((await roles.ListAsync(HttpContext.RequestAborted)).Select(role => new RoleDto(role.RoleName, role.Kind, role.Provider, role.Description)).ToList()));

    /// <summary>A user's derived roles with the relationship that grants each (requirement 47),
    /// for the user editor, read-only: changing the relationship is the only way to change one.</summary>
    public Task<IResult> DerivedRolesAsync(string userId) => RunAsync(async () =>
    {
        if (!await AllowsAsync(UsersPermissions.ListUsers.Name) && callers.Current?.UserId != userId)
        {
            return NotFound();
        }

        var grants = await roles.DerivedRolesAsync(new DerivedRoleContext(userId, null, AllOrganizations: true), HttpContext.RequestAborted);
        return Ok(grants.Select(grant => new DerivedRoleDto(grant.RoleName, grant.Relationship, grant.OrganizationId, grant.RelationshipId)).ToList());
    });

    public Task<IResult> CommentsAsync(string kind, string? id) => RunAsync(async () =>
        Ok(await comments.ListAsync(Target(kind, id), HttpContext.RequestAborted)));

    public Task<IResult> AddCommentAsync(string kind, string? id, ShareCommentRequest request) => RunAsync(async () =>
        Ok(await comments.AddAsync(Target(kind, id), request.Text, request.ReplyTo, HttpContext.RequestAborted)));

    public Task<IResult> RemoveCommentAsync(string kind, string? id, string commentId) => RunAsync(async () =>
    {
        await comments.RemoveAsync(Target(kind, id), commentId, HttpContext.RequestAborted);
        return NoContent();
    });

    private static ShareableRef Target(string kind, string? id) =>
        string.Equals(kind, ShareableRef.TenantRoot.Kind, StringComparison.Ordinal)
            ? ShareableRef.TenantRoot
            : string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id)
                ? throw new ArgumentException("An object is named by its kind and id.")
                : new ShareableRef(kind, id);

    private async Task<bool> AllowsAsync(string permission) =>
        callers.Current is { } caller && (caller.IsSuperUser || (await decision.DecideAsync(caller, permission, cancellationToken: HttpContext.RequestAborted)).IsAllowed);

    private async Task<Dictionary<SharePrincipal, string>> NamesAsync(IReadOnlyCollection<SharePrincipal> principals)
    {
        var names = new Dictionary<SharePrincipal, string>();
        var distinct = principals.Distinct().ToList();
        foreach (var directory in directories)
        {
            foreach (var (principal, name) in await directory.NamesAsync(distinct, HttpContext.RequestAborted))
            {
                names.TryAdd(principal, name);
            }
        }

        return names;
    }

    private static string Name(Dictionary<SharePrincipal, string> names, SharePrincipal principal) =>
        names.TryGetValue(principal, out var name) ? name : principal.ToString();

    private static SharePrincipalDto Principal(SharePrincipal principal) => new(principal.Kind, principal.Id, principal.OrganizationId);

    private static SharePrincipal Principal(SharePrincipalDto principal) =>
        new(principal.Kind, string.IsNullOrEmpty(principal.Id) ? null : principal.Id, string.IsNullOrEmpty(principal.OrganizationId) ? null : principal.OrganizationId);

    private static ShareRuleDto Rule(ShareRule rule) => new(
        rule.Id,
        rule.Name,
        [.. rule.Conditions.Select(condition => new ShareConditionDto(condition.Fact, condition.Operator, [.. condition.Values]))],
        [.. rule.Effects.Select(effect => new ShareEntryDto(Principal(effect.Principal), effect.Principal.ToString(), effect.Level, effect.IsBlock, effect.Starts, effect.Ends))],
        rule.Starts,
        rule.Ends);

    private static ShareRule Rule(ShareRuleDto rule) => new(
        string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("n") : rule.Id,
        rule.Name ?? string.Empty,
        [.. (rule.Conditions ?? []).Select(condition => new ShareCondition(condition.Fact, condition.Operator, [.. condition.Values ?? []]))],
        [.. (rule.Effects ?? []).Select(effect => new ShareEntry(Principal(effect.Principal), effect.IsBlock ? AccessLevel.None : effect.Level, effect.IsBlock, effect.Starts, effect.Ends))],
        rule.Starts,
        rule.Ends);

    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (AccessDeniedException exception)
        {
            // What the caller cannot view does not exist for them (requirement 68).
            return exception.Message == "Not found." ? NotFound() : StatusCode(StatusCodes.Status403Forbidden, exception.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}

public sealed record SharePrincipalDto(SharePrincipalKind Kind, string? Id = null, string? OrganizationId = null);

public sealed record ShareObjectDto(string Kind, string Id);

public sealed record ShareEntryDto(SharePrincipalDto Principal, string Name, AccessLevel Level, bool IsBlock, DateTimeOffset? Starts, DateTimeOffset? Ends);

public sealed record ShareEntryRequest(SharePrincipalDto Principal, AccessLevel Level, bool IsBlock, DateTimeOffset? Starts, DateTimeOffset? Ends);

/// <summary>One decision on the chain: who, what, where it is defined, and whether it is the
/// principal's winning one.</summary>
public sealed record ShareAccessDto(SharePrincipalDto Principal, string Name, AccessLevel Level, bool IsBlock, string DefinedOnKind, string DefinedOnId, int Depth, string? RuleId, string? Source, bool Wins);

public sealed record SharePanelDto(
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
    List<ShareEntryDto> Entries,
    List<ShareAccessDto> Access);

public sealed record ShareConditionDto(string Fact, ShareConditionOperator Operator, List<string>? Values);

public sealed record ShareRuleDto(string? Id, string? Name, List<ShareConditionDto>? Conditions, List<ShareEntryDto>? Effects, DateTimeOffset? Starts, DateTimeOffset? Ends);

public sealed record ShareRuleLevelDto(string Kind, string Id, string Name, bool Editable, List<ShareRuleDto> Rules);

public sealed record ShareIndicatorDto(string Kind, string Id, ShareIndicator Indicator);

public sealed record SharePrincipalOptionDto(SharePrincipalDto Principal, string DisplayName, string? Description, bool IsDerived);

public sealed record RoleDto(string RoleName, RoleKind Kind, string? Provider, string? Description);

public sealed record DerivedRoleDto(string RoleName, string Relationship, string? OrganizationId, string? RelationshipId);

public sealed record ShareCommentRequest(string Text, string? ReplyTo);
