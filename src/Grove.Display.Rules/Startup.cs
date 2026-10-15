using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Api;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Modules;
using Grove.Display.Rules.Drivers;
using Grove.Display.Rules.Models;
using Grove.Display.Rules.Services;

namespace Grove.Display.Rules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<ConditionOptions>();

        // Rule services.
        services
            .AddRules()
            .AddSingleton<IConditionIdGenerator, ConditionIdGenerator>()
            .AddTransient<IConfigureOptions<ConditionOperatorOptions>, ConditionOperatorConfigureOptions>()
            .AddScoped<IConditionResolver, ConditionResolver>()
            .AddScoped<IConditionOperatorResolver, ConditionOperatorResolver>()
            .AddScoped<RuleConditionChecker>()
            .AddScoped<IRuleConditionChecker>(sp => sp.GetRequiredService<RuleConditionChecker>());

        // The rule editor's API (the RuleEditor component in Grove.Display.Components edits the
        // conditions; each condition type's editor is a registered component).
        services.AddApiContributor<Api.RulesApi>();

        services.AddRule<AllConditionGroup, AllConditionEvaluator>();
        services.AddRule<AnyConditionGroup, AnyConditionEvaluator>();
        services.AddRule<BooleanCondition, BooleanConditionEvaluator>();
        services.AddRule<HomepageCondition, HomepageConditionEvaluator>();
        services.AddRule<UrlCondition, UrlConditionEvaluator>();
        services.AddRule<CultureCondition, CultureConditionEvaluator>();
        services.AddRule<RoleCondition, RoleConditionEvaluator>();
        services.AddRule<JavascriptCondition, JavascriptConditionEvaluator>();
        services.AddRule<IsAuthenticatedCondition, IsAuthenticatedConditionEvaluator>();
        services.AddRule<IsAnonymousCondition, IsAnonymousConditionEvaluator>();

        // Content type condition: its evaluator records the displayed content types.
        services.AddRuleCondition<ContentTypeCondition, ContentTypeConditionEvaluatorDriver>()
            .AddScoped<IContentDisplayDriver>(sp => sp.GetRequiredService<ContentTypeConditionEvaluatorDriver>());

        // Allows to serialize 'ConditionOperator' derived types
        services.AddRuleConditionOperator<StringEqualsOperator>()
            .AddRuleConditionOperator<StringNotEqualsOperator>()
            .AddRuleConditionOperator<StringStartsWithOperator>()
            .AddRuleConditionOperator<StringNotStartsWithOperator>()
            .AddRuleConditionOperator<StringEndsWithOperator>()
            .AddRuleConditionOperator<StringNotEndsWithOperator>()
            .AddRuleConditionOperator<StringContainsOperator>()
            .AddRuleConditionOperator<StringNotContainsOperator>();
    }
}
