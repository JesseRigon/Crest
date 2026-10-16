using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Metadata.Builders;

public abstract class ContentTypeFieldSetDefinitionBuilder
{
    protected readonly JsonObject _settings;

    public ContentTypeFieldSetDefinition Current { get; protected set; }
    public string Name { get; private set; }
    public string FieldSetName { get; private set; }
    public string TypeName { get; private set; }

    protected ContentTypeFieldSetDefinitionBuilder(ContentTypeFieldSetDefinition part)
    {
        Current = part;
        Name = part.Name;
        FieldSetName = part.FieldSetDefinition.Name;
        TypeName = part.ContentTypeDefinition != null ? part.ContentTypeDefinition.Name : default;
        _settings = part.Settings?.Clone() ?? [];
    }

    public ContentTypeFieldSetDefinitionBuilder WithSettings<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var jObject = JObject.FromObject(settings, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        _settings[typeof(T).Name] = jObject;

        return this;
    }

    /// <summary>Replaces every settings section with <paramref name="settings"/> (the admin's JSON settings editor).</summary>
    public ContentTypeFieldSetDefinitionBuilder ReplaceSettings(JsonObject settings)
    {
        _settings.Clear();
        _settings.Merge(settings?.DeepClone() as JsonObject ?? [], ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentTypeFieldSetDefinitionBuilder MergeSettings(JsonObject settings)
    {
        _settings.Merge(settings, ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentTypeFieldSetDefinitionBuilder MergeSettings<T>(Action<T> setting) where T : class, new()
    {
        var existingJObject = _settings[typeof(T).Name] as JsonObject;

        // If existing settings do not exist, create.
        if (existingJObject == null)
        {
            existingJObject = JObject.FromObject(new T(), ContentBuilderSettings.IgnoreDefaultValuesSerializer);
            _settings[typeof(T).Name] = existingJObject;
        }

        var settingsToMerge = existingJObject.ToObject<T>();
        setting(settingsToMerge);
        _settings[typeof(T).Name] = JObject.FromObject(settingsToMerge, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        return this;
    }

    public abstract ContentTypeFieldSetDefinition Build();
}
