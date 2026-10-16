using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Management.Metadata.Builders;

/// <summary>Configures an embedding on a type (<see cref="ContentTypeDefinitionBuilder.WithEmbedding(string, Action{EmbeddedContentDefinitionBuilder})"/>).</summary>
public sealed class EmbeddedContentDefinitionBuilder
{
    private readonly EmbeddedContentDefinitionRecord _record;

    public EmbeddedContentDefinitionBuilder(EmbeddedContentDefinitionRecord record)
    {
        _record = record;
        _record.Settings ??= [];
    }

    public string Name => _record.Name;

    public EmbeddedContentDefinitionBuilder WithDisplayName(string displayName)
    {
        _record.DisplayName = displayName;
        return this;
    }

    public EmbeddedContentDefinitionBuilder WithDescription(string description)
    {
        _record.Description = description;
        return this;
    }

    public EmbeddedContentDefinitionBuilder WithPosition(string position)
    {
        _record.Position = position;
        return this;
    }

    /// <summary>The allowed content types; <c>@Stereotype</c> allows every type of that stereotype. Replaces the list.</summary>
    public EmbeddedContentDefinitionBuilder Allow(params string[] typesOrStereotypes)
    {
        _record.AllowedTypes = [.. typesOrStereotypes.Where(type => !string.IsNullOrWhiteSpace(type)).Select(type => type.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        return this;
    }

    public EmbeddedContentDefinitionBuilder WithCount(int? min, int? max)
    {
        _record.MinCount = min;
        _record.MaxCount = max;
        return this;
    }

    public EmbeddedContentDefinitionBuilder Ordered(bool ordered = true)
    {
        _record.Ordered = ordered;
        return this;
    }

    public EmbeddedContentDefinitionBuilder WithDisplayType(string displayType)
    {
        _record.DisplayType = displayType;
        return this;
    }

    /// <summary>The items nest into a tree: each holds its child items under the embedding's name.</summary>
    public EmbeddedContentDefinitionBuilder Nested(bool nested = true)
    {
        _record.Nested = nested;
        return this;
    }

    public EmbeddedContentDefinitionBuilder Collapsed(bool collapsed = true)
    {
        _record.Collapsed = collapsed;
        return this;
    }

    public EmbeddedContentDefinitionBuilder Referenceable(bool referenceable = true)
    {
        _record.Referenceable = referenceable;
        return this;
    }

    /// <summary>The items read through their type's current definition.</summary>
    public EmbeddedContentDefinitionBuilder Live()
    {
        _record.Mode = EmbeddingModes.Live;
        _record.CopiedValues = [];
        return this;
    }

    /// <summary>The items freeze when the parent is published; option list field references copy <paramref name="copiedValues"/> beside their ids.</summary>
    public EmbeddedContentDefinitionBuilder Snapshot(params string[] copiedValues)
    {
        _record.Mode = EmbeddingModes.Snapshot;
        _record.CopiedValues = [.. copiedValues.Where(value => !string.IsNullOrWhiteSpace(value))];
        return this;
    }

    public EmbeddedContentDefinitionBuilder WithSettings<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _record.Settings[typeof(T).Name] = JObject.FromObject(settings, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        return this;
    }

    public EmbeddedContentDefinitionBuilder MergeSettings<T>(Action<T> setting) where T : class, new()
    {
        var existing = _record.Settings[typeof(T).Name] as JsonObject ?? JObject.FromObject(new T(), ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        var settings = existing.ToObject<T>();
        setting(settings);
        _record.Settings[typeof(T).Name] = JObject.FromObject(settings, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        return this;
    }

    /// <summary>Takes every value of <paramref name="record"/> (a recipe's or an API's declaration).</summary>
    public EmbeddedContentDefinitionBuilder From(EmbeddedContentDefinitionRecord record)
    {
        _record.DisplayName = record.DisplayName;
        _record.Description = record.Description;
        _record.Position = record.Position;
        _record.AllowedTypes = [.. record.AllowedTypes ?? []];
        _record.MinCount = record.MinCount;
        _record.MaxCount = record.MaxCount;
        _record.Ordered = record.Ordered;
        _record.DisplayType = record.DisplayType;
        _record.Nested = record.Nested;
        _record.Collapsed = record.Collapsed;
        _record.Referenceable = record.Referenceable;
        _record.Mode = EmbeddingModes.Normalize(record.Mode);
        _record.CopiedValues = [.. record.CopiedValues ?? []];
        if (record.Settings is not null)
        {
            _record.Settings.Merge(record.Settings, ContentBuilderSettings.JsonMergeSettings);
        }

        return this;
    }

    public EmbeddedContentDefinition Build() => new(_record);
}
