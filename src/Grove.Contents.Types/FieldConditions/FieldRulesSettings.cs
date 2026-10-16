#nullable enable

using Grove.Display.Management.Components;

namespace Grove.Contents.Types;

/// <summary>
/// A type's rules across fields (docs/content-engine.md › Validation and conditions run on the one
/// expression engine), a type setting: each rule's <see cref="FieldRule.When"/> is a field condition
/// (<see cref="FieldConditions"/>), evaluated when the item is validated.
/// </summary>
public class FieldRulesSettings
{
    public List<FieldRule> Rules { get; set; } = [];
}

/// <summary>While <see cref="When"/> holds, <see cref="Effect"/> applies to <see cref="Field"/>.</summary>
public class FieldRule
{
    public DisplayBinding? When { get; set; }

    /// <summary>The field path (<c>FieldSet.Field</c>) the effect applies to and the error is keyed by.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>See <see cref="FieldRuleEffects"/>.</summary>
    public string Effect { get; set; } = FieldRuleEffects.Require;

    /// <summary>The error shown under the field; a required field without one gets the standard message.</summary>
    public string? Message { get; set; }
}

public static class FieldRuleEffects
{
    /// <summary>The field needs a value (unless its visibility hides it).</summary>
    public const string Require = "require";

    /// <summary>The save is refused with the rule's message under the field.</summary>
    public const string Refuse = "refuse";

    public static readonly string[] All = [Require, Refuse];
}
