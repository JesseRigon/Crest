using System.Text.Json.Nodes;
using Grove.Display.Rules.Models;
using Jint;
using Jint.Runtime;
using Microsoft.Extensions.Localization;

namespace Grove.Display.Rules.Services;

/// <summary>
/// Checks a rule's conditions as the rule editor sends them (their JSON, groups nested): every
/// condition is a registered type, a string condition names its operator, and a JavaScript
/// condition holds a script that parses and evaluates to true or false. The problems are keyed by
/// condition id, so the editor shows each under its condition.
/// </summary>
public sealed class RuleConditionChecker : IRuleConditionChecker
{
    private readonly HashSet<string> _known;
    private readonly JavascriptConditionEvaluator _javascript;

    internal readonly IStringLocalizer S;

    public RuleConditionChecker(
        IEnumerable<IConditionFactory> factories,
        JavascriptConditionEvaluator javascript,
        IStringLocalizer<RuleConditionChecker> stringLocalizer)
    {
        _known = factories.Select(factory => factory.Name).ToHashSet(StringComparer.Ordinal);
        _javascript = javascript;
        S = stringLocalizer;
    }

    public async Task<Dictionary<string, string>> CheckAsync(JsonArray? conditions)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        await CheckAsync(conditions, string.Empty, errors);
        return errors;
    }

    private async Task CheckAsync(JsonArray? conditions, string path, Dictionary<string, string> errors)
    {
        for (var index = 0; index < (conditions?.Count ?? 0); index++)
        {
            var at = path.Length == 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{path}.{index}";
            if (conditions![index] is not JsonObject condition)
            {
                errors[at] = S["A condition must be an object."];
                continue;
            }

            var key = condition["ConditionId"] is JsonValue id && id.TryGetValue<string>(out var conditionId) && !string.IsNullOrEmpty(conditionId) ? conditionId : at;
            var name = condition["Name"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            if (name is null || !_known.Contains(name))
            {
                errors[key] = S["The condition '{0}' is not available (is its feature enabled?).", name ?? string.Empty];
                continue;
            }

            if (condition["Conditions"] is JsonArray children)
            {
                await CheckAsync(children, at, errors);
            }

            switch (name)
            {
                case nameof(UrlCondition) or nameof(CultureCondition) or nameof(RoleCondition) or nameof(ContentTypeCondition):
                    if (condition["Operation"] is not JsonObject)
                    {
                        errors[key] = S["Select the condition operator."];
                    }

                    break;

                case nameof(JavascriptCondition):
                    if (await CheckScriptAsync(condition["Script"] is JsonValue script && script.TryGetValue<string>(out var source) ? source : null) is { } problem)
                    {
                        errors[key] = problem;
                    }

                    break;
            }
        }
    }

    /// <summary>Why <paramref name="script"/> is not a usable JavaScript condition, or null when it is.</summary>
    public async Task<string?> CheckScriptAsync(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return S["Please provide a script."];
        }

        try
        {
            _ = await _javascript.EvaluateAsync(new JavascriptCondition { Name = nameof(JavascriptCondition), Script = script });
            return null;
        }
        catch (ScriptPreparationException ex) // Invalid syntax
        {
            // Jint wraps the parser's exception; the inner one carries the error and its position.
            return S["The script couldn't be parsed. Details: {0}", (ex.InnerException ?? ex).Message];
        }
        catch (JavaScriptException ex) // Evaluation threw an Error
        {
            return S["JavaScript evaluation resulted in an exception. Details: {0}", ex.Message];
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException) // The result is not a boolean
        {
            return S["The script evaluation failed. Details: {0}", ex.Message];
        }
    }
}
