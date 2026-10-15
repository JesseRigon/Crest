using System.Text.Json.Nodes;

namespace Grove.Components.Models;

/// <summary>A settings group's form as <c>api/grove/settings/groups/{id}</c> serves it.</summary>
public sealed record SettingsFormModel(SettingsSchemaModel Schema, JsonObject Values, Dictionary<string, bool>? Secrets);

public sealed record SettingsSchemaModel(string GroupId, string Caption, string? Description, bool ReloadsTenant, IReadOnlyList<SettingsFieldModel> Fields);

/// <summary>One field of a settings form (Grove.Settings' <c>SettingsField</c>).</summary>
public sealed record SettingsFieldModel
{
    public string Name { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Kind { get; init; } = "text";

    public string? Description { get; init; }

    public string? Section { get; init; }

    public string? Placeholder { get; init; }

    public bool Required { get; init; }

    public bool ReadOnly { get; init; }

    public bool Secret { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public int? MaxLength { get; init; }

    public string? Language { get; init; }

    public SettingsFieldConditionModel? VisibleWhen { get; init; }

    public IReadOnlyList<SettingOptionModel>? Options { get; init; }

    public IReadOnlyList<SettingsFieldModel>? Fields { get; init; }
}

public sealed record SettingsFieldConditionModel(string Field, IReadOnlyList<string>? Values);

public sealed record SettingOptionModel(string Value, string Text);

/// <summary>What a save answers.</summary>
public sealed record SettingsSavedModel(JsonObject Values, Dictionary<string, bool>? Secrets, bool Changed, bool Reloaded);
