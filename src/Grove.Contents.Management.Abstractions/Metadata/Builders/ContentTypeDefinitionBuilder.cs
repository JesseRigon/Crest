using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Utilities;

namespace Grove.Contents.Management.Metadata.Builders;

public class ContentTypeDefinitionBuilder
{
    private string _name;
    private string _displayName;
    private readonly List<ContentTypeFieldSetDefinition> _parts;
    private readonly List<EmbeddedContentDefinition> _embeddings;
    private readonly JsonObject _settings;

    public ContentTypeDefinition Current { get; }

    public ContentTypeDefinitionBuilder()
        : this(new ContentTypeDefinition(null, null))
    {
    }

    public ContentTypeDefinitionBuilder(ContentTypeDefinition existing)
    {
        Current = existing;

        if (existing == null)
        {
            _parts = [];
            _embeddings = [];
            _settings = [];
        }
        else
        {
            _name = existing.Name;
            _displayName = existing.DisplayName;
            _parts = existing.FieldSets.ToList();
            _embeddings = existing.Embeddings.ToList();
            _settings = existing.Settings.Clone();
        }
    }

    public ContentTypeDefinition Build()
    {
        if (!char.IsLetter(_name[0]))
        {
            throw new ArgumentException("Content type name must start with a letter", "name");
        }
        if (!string.Equals(_name, _name.ToSafeName(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Content type name contains invalid characters", "name");
        }
        if (_name.IsReservedContentName())
        {
            throw new ArgumentException("Content type name is reserved for internal use", "name");
        }

        foreach (var embedding in _embeddings)
        {
            if (_parts.Any(part => string.Equals(part.Name, embedding.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"The embedding '{embedding.Name}' has the name of a field-set attachment of '{_name}'.", "name");
            }
        }

        return new ContentTypeDefinition(_name, _displayName, _parts, _settings, _embeddings);
    }

    [Obsolete("This method has been deprecated, please use WithName() instead.")]
    public ContentTypeDefinitionBuilder Named(string name) => WithName(name);

    public ContentTypeDefinitionBuilder WithName(string name)
    {
        _name = name;

        return this;
    }

    [Obsolete("This method has been deprecated, please use WithDisplayName() instead.")]
    public ContentTypeDefinitionBuilder DisplayedAs(string displayName) => WithDisplayName(displayName);

    public ContentTypeDefinitionBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;

        return this;
    }

    /// <summary>Replaces every settings section with <paramref name="settings"/> (the admin's JSON settings editor).</summary>
    public ContentTypeDefinitionBuilder ReplaceSettings(JsonObject settings)
    {
        _settings.Clear();
        _settings.Merge(settings?.DeepClone() as JsonObject ?? [], ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentTypeDefinitionBuilder MergeSettings(JsonObject settings)
    {
        _settings.Merge(settings, ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentTypeDefinitionBuilder MergeSettings<T>(Action<T> setting) where T : class, new()
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

    public ContentTypeDefinitionBuilder WithSettings<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var jObject = JObject.FromObject(settings, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        _settings[typeof(T).Name] = jObject;

        return this;
    }

    /// <summary>Declares or reconfigures the embedding <paramref name="name"/> (docs/content-engine.md › Embedded content items).</summary>
    public ContentTypeDefinitionBuilder WithEmbedding(string name, Action<EmbeddedContentDefinitionBuilder> configuration)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0]) || !string.Equals(name, name.ToSafeName(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("An embedding name must start with a letter and contain only letters and digits.", nameof(name));
        }

        var index = _embeddings.FindIndex(embedding => string.Equals(embedding.Name, name, StringComparison.OrdinalIgnoreCase));
        var record = index >= 0 ? _embeddings[index].ToRecord() : new Records.EmbeddedContentDefinitionRecord { Name = name };
        var builder = new EmbeddedContentDefinitionBuilder(record);
        configuration(builder);

        var built = builder.Build();
        built.ContentTypeDefinition = Current;
        if (index >= 0)
        {
            _embeddings[index] = built;
        }
        else
        {
            _embeddings.Add(built);
        }

        return this;
    }

    public ContentTypeDefinitionBuilder RemoveEmbedding(string name)
    {
        _embeddings.RemoveAll(embedding => string.Equals(embedding.Name, name, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    public ContentTypeDefinitionBuilder RemoveFieldSet<TFieldSet>()
        where TFieldSet : ContentFieldSet
        => RemoveFieldSet(ContentFieldSetName.Of<TFieldSet>());

    public ContentTypeDefinitionBuilder RemoveFieldSet(string fieldSetName)
    {
        var existingFieldSet = _parts.SingleOrDefault(x => string.Equals(x.Name, fieldSetName, StringComparison.OrdinalIgnoreCase));
        if (existingFieldSet != null)
        {
            _parts.Remove(existingFieldSet);
        }
        return this;
    }

    public ContentTypeDefinitionBuilder WithFieldSet(string fieldSetName)
        => WithFieldSet(fieldSetName, configuration => { });

    public ContentTypeDefinitionBuilder WithFieldSet(string name, string fieldSetName)
        => WithFieldSet(name, new ContentFieldSetDefinition(fieldSetName), configuration => { });

    public ContentTypeDefinitionBuilder WithFieldSet(string fieldSetName, Action<ContentTypeFieldSetDefinitionBuilder> configuration)
        => WithFieldSet(fieldSetName, new ContentFieldSetDefinition(fieldSetName), configuration);

    public ContentTypeDefinitionBuilder WithFieldSet(string name, ContentFieldSetDefinition fieldSetDefinition, Action<ContentTypeFieldSetDefinitionBuilder> configuration)
    {
        var existingFieldSet = _parts.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingFieldSet != null)
        {
            _parts.Remove(existingFieldSet);
        }
        else
        {
            existingFieldSet = new ContentTypeFieldSetDefinition(name, fieldSetDefinition, [])
            {
                ContentTypeDefinition = Current,
            };
        }

        var configurer = new FieldSetConfigurerImpl(existingFieldSet);
        configuration(configurer);
        _parts.Add(configurer.Build());
        return this;
    }

    public ContentTypeDefinitionBuilder WithFieldSet(string name, string fieldSetName, Action<ContentTypeFieldSetDefinitionBuilder> configuration)
        => WithFieldSet(name, new ContentFieldSetDefinition(fieldSetName), configuration);

    public ContentTypeDefinitionBuilder WithFieldSet<TFieldSet>() where TFieldSet : ContentFieldSet
        => WithFieldSet(ContentFieldSetName.Of<TFieldSet>(), configuration => { });

    public ContentTypeDefinitionBuilder WithFieldSet<TFieldSet>(string name) where TFieldSet : ContentFieldSet
        => WithFieldSet(name, new ContentFieldSetDefinition(ContentFieldSetName.Of<TFieldSet>()), configuration => { });

    public ContentTypeDefinitionBuilder WithFieldSet<TFieldSet>(string name, Action<ContentTypeFieldSetDefinitionBuilder> configuration) where TFieldSet : ContentFieldSet
        => WithFieldSet(name, new ContentFieldSetDefinition(ContentFieldSetName.Of<TFieldSet>()), configuration);

    public ContentTypeDefinitionBuilder WithFieldSet<TFieldSet>(Action<ContentTypeFieldSetDefinitionBuilder> configuration) where TFieldSet : ContentFieldSet
        => WithFieldSet(ContentFieldSetName.Of<TFieldSet>(), configuration);

    public Task<ContentTypeDefinitionBuilder> WithFieldSetAsync(string name, string fieldSetName, Func<ContentTypeFieldSetDefinitionBuilder, Task> configurationAsync)
        => WithFieldSetAsync(name, new ContentFieldSetDefinition(fieldSetName), configurationAsync);

    public Task<ContentTypeDefinitionBuilder> WithFieldSetAsync(string fieldSetName, Func<ContentTypeFieldSetDefinitionBuilder, Task> configurationAsync)
        => WithFieldSetAsync(fieldSetName, new ContentFieldSetDefinition(fieldSetName), configurationAsync);

    public async Task<ContentTypeDefinitionBuilder> WithFieldSetAsync(string name, ContentFieldSetDefinition fieldSetDefinition, Func<ContentTypeFieldSetDefinitionBuilder, Task> configurationAsync)
    {
        var existingFieldSet = _parts.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existingFieldSet != null)
        {
            _parts.Remove(existingFieldSet);
        }
        else
        {
            existingFieldSet = new ContentTypeFieldSetDefinition(name, fieldSetDefinition, [])
            {
                ContentTypeDefinition = Current,
            };
        }

        var configurer = new FieldSetConfigurerImpl(existingFieldSet);

        await configurationAsync(configurer);

        _parts.Add(configurer.Build());

        return this;
    }

    private sealed class FieldSetConfigurerImpl : ContentTypeFieldSetDefinitionBuilder
    {
        private readonly ContentFieldSetDefinition _partDefinition;

        public FieldSetConfigurerImpl(ContentTypeFieldSetDefinition part)
            : base(part)
        {
            Current = part;
            _partDefinition = part.FieldSetDefinition;
        }

        public override ContentTypeFieldSetDefinition Build()
        {
            if (!char.IsLetter(Current.Name[0]))
            {
                throw new ArgumentException("Field set name must start with a letter", "name");
            }

            if (!string.Equals(Current.Name, Current.Name.ToSafeName(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Field set name contains invalid characters", "name");
            }

            return new ContentTypeFieldSetDefinition(Current.Name, _partDefinition, _settings)
            {
                ContentTypeDefinition = Current.ContentTypeDefinition,
            };
        }
    }
}
