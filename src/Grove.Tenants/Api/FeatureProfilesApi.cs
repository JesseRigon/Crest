#nullable enable

using Grove.Api;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Models;
using Grove.Tenants.Services;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Tenants.Api;

/// <summary>
/// <c>api/grove/tenants/feature-profiles</c>: the feature profiles a tenant can be given (which
/// features it may enable), on the Default tenant, with <c>ManageTenantFeatureProfiles</c>.
/// </summary>
public sealed class FeatureProfilesApi : ApiContributor
{
    public override string Name => "FeatureProfiles";

    public override string FeatureId => "Grove.Tenants.FeatureProfiles";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("tenants/feature-profiles");
        routes.MapGet("", ApiHandlers.Handle<FeatureProfilesHandlers>(h => h.ListAsync()));
        routes.MapPost("", (HttpContext http, [FromBody] FeatureProfileWrite request) => ApiHandlers.For<FeatureProfilesHandlers>(http).SaveAsync(null, request));
        routes.MapPut("{id}", (HttpContext http, [FromRoute] string id, [FromBody] FeatureProfileWrite request) => ApiHandlers.For<FeatureProfilesHandlers>(http).SaveAsync(id, request));
        routes.MapDelete("{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<FeatureProfilesHandlers>(http).DeleteAsync(id));
    }
}

/// <summary>A feature rule: <see cref="Rule"/> (<c>Include</c>, <c>Exclude</c>, ...) applied to the feature ids <see cref="Expression"/> matches.</summary>
public sealed record FeatureRuleModel(string Rule, string Expression);

public sealed record FeatureProfileModel(string Id, string Name, FeatureRuleModel[] FeatureRules);

/// <summary>The profiles and the rules a feature rule can name.</summary>
public sealed record FeatureProfilesModel(FeatureProfileModel[] Profiles, string[] Rules);

/// <summary>A profile as the page writes it. <see cref="Id"/> is its key, set on create.</summary>
public sealed class FeatureProfileWrite
{
    public string? Id { get; set; }

    [NotEmpty]
    public string Name { get; set; } = string.Empty;

    public FeatureRuleModel[] FeatureRules { get; set; } = [];
}

public sealed class FeatureProfilesHandlers(
    IAuthorizationService authorization,
    ShellSettings shellSettings,
    FeatureProfilesManager profiles,
    IOptions<FeatureProfilesRuleOptions> ruleOptions) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        var document = await profiles.GetFeatureProfilesDocumentAsync();
        return Ok(new FeatureProfilesModel(
            [.. document.FeatureProfiles.OrderBy(entry => entry.Value.Name ?? entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => ToModel(entry.Key, entry.Value))],
            [.. ruleOptions.Value.Rules.Keys.Order(StringComparer.OrdinalIgnoreCase)]));
    }

    /// <summary>Creates a profile (<paramref name="id"/> null) or replaces one.</summary>
    public async Task<IResult> SaveAsync(string? id, FeatureProfileWrite request)
    {
        if (!await CanManageAsync()) return Forbid();

        var document = await profiles.LoadFeatureProfilesDocumentAsync();
        if (id is not null && !document.FeatureProfiles.ContainsKey(id))
        {
            return NotFound();
        }

        var key = id ?? request.Id?.Trim();
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(key))
        {
            errors.Add("id", "The id is required.");
        }
        else if (id is null && document.FeatureProfiles.ContainsKey(key))
        {
            errors.Add("id", "A feature profile with the same id already exists.");
        }

        var name = request.Name.Trim();
        if (document.FeatureProfiles.Any(entry => !string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase) && string.Equals(entry.Value.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("name", "A feature profile with the same name already exists.");
        }

        for (var index = 0; index < request.FeatureRules.Length; index++)
        {
            var rule = request.FeatureRules[index];
            if (!ruleOptions.Value.Rules.ContainsKey(rule.Rule ?? string.Empty))
            {
                errors.Add($"featureRules[{index}].rule", $"'{rule.Rule}' is not a feature rule.");
            }

            if (string.IsNullOrWhiteSpace(rule.Expression))
            {
                errors.Add($"featureRules[{index}].expression", "The expression is required.");
            }
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var profile = new FeatureProfile
        {
            Id = key,
            Name = name,
            FeatureRules = [.. request.FeatureRules.Select(rule => new FeatureRule { Rule = rule.Rule, Expression = rule.Expression.Trim() })],
        };

        await profiles.UpdateFeatureProfileAsync(key!, profile);
        return Ok(ToModel(key!, profile));
    }

    public async Task<IResult> DeleteAsync(string id)
    {
        if (!await CanManageAsync()) return Forbid();

        var document = await profiles.LoadFeatureProfilesDocumentAsync();
        if (!document.FeatureProfiles.ContainsKey(id))
        {
            return NotFound();
        }

        await profiles.RemoveFeatureProfileAsync(id);
        return NoContent();
    }

    private static FeatureProfileModel ToModel(string id, FeatureProfile profile) =>
        new(id, profile.Name ?? id, [.. (profile.FeatureRules ?? []).Select(rule => new FeatureRuleModel(rule.Rule, rule.Expression))]);

    private async Task<bool> CanManageAsync() =>
        shellSettings.IsDefaultShell() && await authorization.AuthorizeAsync(User, Permissions.ManageTenantFeatureProfiles);
}
