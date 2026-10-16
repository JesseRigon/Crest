using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Metadata.Builders;

public abstract class ContentFieldDefinitionBuilder
{
    protected readonly JsonObject _settings;

    public ContentFieldDefinition Current { get; private set; }
    public abstract string Name { get; }
    public abstract string FieldType { get; }
    public abstract string FieldSetName { get; }

    protected ContentFieldDefinitionBuilder(ContentFieldDefinition field)
    {
        Current = field;

        _settings = field.Settings.Clone();
    }

    /// <summary>Replaces every settings section with <paramref name="settings"/> (the admin's JSON settings editor).</summary>
    public ContentFieldDefinitionBuilder ReplaceSettings(JsonObject settings)
    {
        _settings.Clear();
        _settings.Merge(settings?.DeepClone() as JsonObject ?? [], ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentFieldDefinitionBuilder MergeSettings(JsonObject settings)
    {
        _settings.Merge(settings, ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentFieldDefinitionBuilder MergeSettings<T>(Action<T> setting) where T : class, new()
    {
        var existingJObject = _settings[typeof(T).Name] as JsonObject;
        // If existing settings do not exist, create.
        if (existingJObject == null)
        {
            existingJObject = ToJsonObject(new T());
            _settings[typeof(T).Name] = existingJObject;
        }

        var settingsToMerge = existingJObject.ToObject<T>();
        setting(settingsToMerge);
        _settings[typeof(T).Name] = ToJsonObject(settingsToMerge);
        return this;
    }

    public ContentFieldDefinitionBuilder WithSettings<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings[typeof(T).Name] = ToJsonObject(settings);

        return this;
    }

    public ContentFieldDefinitionBuilder WithSettings<T>() where T : class, new()
    {
        _settings[typeof(T).Name] = ToJsonObject(new T());

        return this;
    }

    public abstract ContentFieldDefinitionBuilder OfType(ContentFieldTypeDefinition fieldTypeDefinition);
    public abstract ContentFieldDefinitionBuilder OfType(string fieldType);
    public abstract ContentFieldDefinition Build();

    private static JsonObject ToJsonObject(object obj)
        => JObject.FromObject(obj, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
}
