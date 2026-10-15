using System.Text.Json.Nodes;

namespace Grove.Display.Rules.Services;

/// <summary>
/// Checks a rule's conditions as an editor sends them (their JSON, groups nested) before they are
/// stored: every condition is a registered type, a string condition names its operator, and a
/// JavaScript condition holds a script that parses and evaluates to true or false.
/// </summary>
public interface IRuleConditionChecker
{
    /// <summary>The problems, keyed by condition id (or the condition's index path when it has none); empty when the conditions are usable.</summary>
    Task<Dictionary<string, string>> CheckAsync(JsonArray? conditions);
}
