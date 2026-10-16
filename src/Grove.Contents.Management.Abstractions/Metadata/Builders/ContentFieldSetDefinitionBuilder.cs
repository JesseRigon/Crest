using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Management.Utilities;

namespace Grove.Contents.Management.Metadata.Builders;

public class ContentFieldSetDefinitionBuilder
{
    private readonly ContentFieldSetDefinition _part;
    private readonly List<ContentFieldDefinition> _fields;
    private readonly JsonObject _settings;

    public ContentFieldSetDefinition Current { get; private set; }

    public ContentFieldSetDefinitionBuilder()
        : this(new ContentFieldSetDefinition(null))
    {
    }

    public ContentFieldSetDefinitionBuilder(ContentFieldSetDefinition existing)
    {
        _part = existing;

        if (existing == null)
        {
            _fields = [];
            _settings = [];
        }
        else
        {
            Name = existing.Name;
            _fields = existing.Fields.ToList();
            _settings = existing.Settings.Clone();
        }
    }

    public string Name { get; private set; }

    public ContentFieldSetDefinition Build()
    {
        if (!char.IsLetter(Name[0]))
        {
            throw new ArgumentException("Field set name must start with a letter", "name");
        }
        if (!string.Equals(Name, Name.ToSafeName(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Field set name contains invalid characters", "name");
        }
        if (Name.IsReservedContentName())
        {
            throw new ArgumentException("Field set name is reserved for internal use", "name");
        }

        return new ContentFieldSetDefinition(Name, _fields, _settings);
    }

    public ContentFieldSetDefinitionBuilder Named(string name)
    {
        Name = name;

        return this;
    }

    public ContentFieldSetDefinitionBuilder RemoveField(string fieldName)
    {
        var existingField = _fields.SingleOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (existingField != null)
        {
            _fields.Remove(existingField);
        }

        return this;
    }

    /// <summary>Replaces every settings section with <paramref name="settings"/> (the admin's JSON settings editor).</summary>
    public ContentFieldSetDefinitionBuilder ReplaceSettings(JsonObject settings)
    {
        _settings.Clear();
        _settings.Merge(settings?.DeepClone() as JsonObject ?? [], ContentBuilderSettings.JsonMergeSettings);
        return this;
    }

    public ContentFieldSetDefinitionBuilder MergeSettings(JsonObject settings)
    {
        _settings.Merge(settings, ContentBuilderSettings.JsonMergeSettings);

        return this;
    }

    public ContentFieldSetDefinitionBuilder MergeSettings<T>(Action<T> setting) where T : class, new()
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

    public ContentFieldSetDefinitionBuilder WithSettings<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));

        var jObject = JObject.FromObject(settings, ContentBuilderSettings.IgnoreDefaultValuesSerializer);
        _settings[typeof(T).Name] = jObject;

        return this;
    }

    public ContentFieldSetDefinitionBuilder WithField(string fieldName)
        => WithField(fieldName, configuration => { });

    public ContentFieldSetDefinitionBuilder WithField(string fieldName, Action<ContentFieldDefinitionBuilder> configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));

        var existingField = _fields.FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (existingField != null)
        {
            var toRemove = _fields.Where(x => x.Name == fieldName).ToArray();
            foreach (var remove in toRemove)
            {
                _fields.Remove(remove);
            }
        }
        else
        {
            existingField = new ContentFieldDefinition(null, fieldName, []);
        }

        var configurer = new FieldConfigurerImpl(existingField, _part);

        configuration(configurer);

        var fieldDefinition = configurer.Build();

        var settings = fieldDefinition.GetSettings<ContentFieldSettings>();

        if (string.IsNullOrEmpty(settings.DisplayName))
        {
            // If there is no display name, let's use the field name by default.
            settings.DisplayName = fieldName;
            fieldDefinition.Settings.Remove(nameof(ContentFieldSettings));
            fieldDefinition.Settings.Add(nameof(ContentFieldSettings), JNode.FromObject(settings));
        }

        _fields.Add(fieldDefinition);

        return this;
    }

    public ContentFieldSetDefinitionBuilder WithField<TField>(string fieldName)
        => WithField(fieldName, configuration => configuration.OfType(typeof(TField).Name));

    public ContentFieldSetDefinitionBuilder WithField<TField>(string fieldName, Action<ContentFieldDefinitionBuilder> configuration)
        => WithField(fieldName, field =>
        {
            configuration(field);

            field.OfType(typeof(TField).Name);
        });

    public Task<ContentFieldSetDefinitionBuilder> WithFieldAsync<TField>(string fieldName, Func<ContentFieldDefinitionBuilder, Task> configuration)
        => WithFieldAsync(fieldName, async field =>
        {
            await configuration(field);

            field.OfType(typeof(TField).Name);
        });

    public async Task<ContentFieldSetDefinitionBuilder> WithFieldAsync(string fieldName, Func<ContentFieldDefinitionBuilder, Task> configurationAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));

        var existingField = _fields.FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        if (existingField != null)
        {
            var toRemove = _fields.Where(x => x.Name == fieldName).ToArray();
            foreach (var remove in toRemove)
            {
                _fields.Remove(remove);
            }
        }
        else
        {
            existingField = new ContentFieldDefinition(null, fieldName, []);
        }

        var configurer = new FieldConfigurerImpl(existingField, _part);

        await configurationAsync(configurer);

        var fieldDefinition = configurer.Build();

        var settings = fieldDefinition.GetSettings<ContentFieldSettings>();

        if (string.IsNullOrEmpty(settings.DisplayName))
        {
            // If there is no display name, let's use the field name by default.
            settings.DisplayName = fieldName;
            fieldDefinition.Settings.Remove(nameof(ContentFieldSettings));
            fieldDefinition.Settings.Add(nameof(ContentFieldSettings), JNode.FromObject(settings));
        }

        _fields.Add(fieldDefinition);

        return this;
    }

    private sealed class FieldConfigurerImpl : ContentFieldDefinitionBuilder
    {
        private ContentFieldTypeDefinition _fieldTypeDefinition;
        private readonly ContentFieldSetDefinition _partDefinition;
        private readonly string _fieldName;

        public FieldConfigurerImpl(ContentFieldDefinition field, ContentFieldSetDefinition part)
            : base(field)
        {
            _fieldTypeDefinition = field.FieldTypeDefinition;
            _fieldName = field.Name;
            _partDefinition = part;
        }

        public override ContentFieldDefinition Build()
        {
            if (!char.IsLetter(_fieldName[0]))
            {
                throw new ArgumentException("Content field name must start with a letter", "name");
            }
            if (!string.Equals(_fieldName, _fieldName.ToSafeName(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Content field name contains invalid characters", "name");
            }

            return new ContentFieldDefinition(_fieldTypeDefinition, _fieldName, _settings);
        }

        public override string Name
            => _fieldName;

        public override string FieldType
            => _fieldTypeDefinition.Name;


        public override string FieldSetName
            => _partDefinition.Name;

        public override ContentFieldDefinitionBuilder OfType(ContentFieldTypeDefinition fieldTypeDefinition)
        {
            _fieldTypeDefinition = fieldTypeDefinition;
            return this;
        }

        public override ContentFieldDefinitionBuilder OfType(string fieldType)
        {
            _fieldTypeDefinition = new ContentFieldTypeDefinition(fieldType);
            return this;
        }
    }
}
