using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Display.Rules.Services;

namespace Grove.Display.Rules;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRules(this IServiceCollection services)
        => services.AddScoped<IRuleService, RuleService>();

    /// <summary>Registers a condition type and its evaluator; its editor is a registered component that <c>Edits</c> the condition's name.</summary>
    public static IServiceCollection AddRule<TCondition, TEvaluator>(this IServiceCollection services)
        where TCondition : Condition, new()
        where TEvaluator : class, IConditionEvaluator
        => services.AddRuleCondition<TCondition, TEvaluator>();

    public static IServiceCollection AddRuleConditionOperator<TOperator>(this IServiceCollection services)
        where TOperator : ConditionOperator
    {
        return services.AddJsonDerivedTypeInfo<TOperator, ConditionOperator>();
    }

    public static IServiceCollection AddRuleCondition<TCondition, TConditionEvaluator>(this IServiceCollection services)
        where TCondition : Condition, new()
        where TConditionEvaluator : class, IConditionEvaluator
    {
        return services.AddRuleCondition<TCondition, TConditionEvaluator, ConditionFactory<TCondition>>();
    }
}
