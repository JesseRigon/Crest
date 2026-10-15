using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Contents.Management.Metadata;
using Grove.Data.Localization;
using Grove.Display.Rules.Models;
using Grove.Display.Rules.Services;
using Grove.Infrastructure.Shells.Admin;
using Grove.Json;
using Grove.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Display.Rules.Api;

/// <summary>
/// <c>api/grove/rules</c>: what a rule editor (the <c>RuleEditor</c> component, Grove.Display.Components)
/// needs to edit a rule's conditions: the condition types the enabled features registered (each
/// with its <c>$type</c>, a new condition's JSON and the values it may compare with), the
/// operators of the string conditions, and a check of a rule's conditions before it is saved.
/// For callers who may open the admin.
/// </summary>
public sealed class RulesApi : ApiContributor
{
    public override string Name => "Rules";

    public override string FeatureId => "Grove.Display.Rules";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("rules");
        routes.MapGet("conditions", ApiHandlers.Handle<RulesHandlers>(h => h.CatalogAsync()));
        routes.MapPost("check", (HttpContext http, [FromBody] RuleCheckRequest request) => ApiHandlers.For<RulesHandlers>(http).CheckAsync(request));
    }
}

/// <summary>A condition type: its name (the <c>Name</c> of its JSON), a new condition of the type, and the values its <c>Value</c> may compare with.</summary>
public sealed record RuleConditionType(string Name, string DisplayName, string Description, string Category, JsonObject Template, string[]? Choices);

/// <summary>An operator a string condition compares with: its name and the JSON of a new one (its <c>$type</c>).</summary>
public sealed record RuleOperator(string Name, string DisplayName, bool Negates, JsonObject Template);

public sealed record RuleCatalog(RuleConditionType[] Conditions, RuleOperator[] Operators);

public sealed record RuleCheckRequest(JsonArray? Conditions);

/// <summary>The conditions' problems by condition id (an index path for a condition without one).</summary>
public sealed record RuleCheck(Dictionary<string, string> Errors);

public sealed class RulesHandlers(
    IEnumerable<IConditionFactory> factories,
    IOptions<ConditionOperatorOptions> operators,
    IOptions<DocumentJsonSerializerOptions> serialization,
    RuleConditionChecker checker,
    IAuthorizationService authorization,
    IServiceProvider services) : ApiHandlers
{
    public async Task<IResult> CatalogAsync()
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel))
        {
            return Forbid();
        }

        var options = serialization.Value.SerializerOptions;
        var conditions = new List<RuleConditionType>();
        foreach (var factory in factories.OrderBy(factory => factory.Name, StringComparer.Ordinal))
        {
            var (displayName, description, category) = RuleConditionDescriptions.Describe(factory.Name);
            var template = JsonSerializer.SerializeToNode(factory.Create(), typeof(Condition), options) as JsonObject ?? [];
            conditions.Add(new(factory.Name, displayName, description, category, template, await ChoicesAsync(factory.Name)));
        }

        var operatorList = operators.Value.Operators
            .Where(option => typeof(StringOperator).IsAssignableFrom(option.Operator))
            .Select(option => new RuleOperator(
                option.Factory.Name,
                option.DisplayText(services).Value,
                typeof(INegateOperator).IsAssignableFrom(option.Operator),
                JsonSerializer.SerializeToNode(option.Factory.Create(), typeof(ConditionOperator), options) as JsonObject ?? []))
            .ToArray();

        return Ok(new RuleCatalog([.. conditions], operatorList));
    }

    public async Task<IResult> CheckAsync(RuleCheckRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel))
        {
            return Forbid();
        }

        return Ok(new RuleCheck(await checker.CheckAsync(request?.Conditions)));
    }

    // The values a condition's Value may take, where the site knows them: its cultures, its
    // roles, its content types. A URL condition compares with any address.
    private async Task<string[]?> ChoicesAsync(string conditionName)
    {
        switch (conditionName)
        {
            case nameof(CultureCondition):
                return services.GetService<ILocalizationService>() is { } localization ? await localization.GetSupportedCulturesAsync() : null;
            case nameof(RoleCondition):
                return services.GetService<Grove.Security.Services.IRoleService>() is { } roles
                    ? [.. (await roles.GetRolesAsync()).Select(role => role.RoleName).Order(StringComparer.OrdinalIgnoreCase)]
                    : null;
            case nameof(ContentTypeCondition):
                return services.GetService<IContentDefinitionManager>() is { } definitions
                    ? [.. (await definitions.ListTypeDefinitionsAsync()).Select(type => type.Name).Order(StringComparer.OrdinalIgnoreCase)]
                    : null;
            default:
                return null;
        }
    }
}
