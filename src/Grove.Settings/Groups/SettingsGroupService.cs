#nullable enable

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Grove.Environment.Shell;
using Grove.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Settings.Groups;

/// <summary>A group's form as the API serves it: the schema, the values, and which secrets are set.</summary>
public sealed record SettingsGroupForm(SettingsSchema Schema, JsonObject Values, IReadOnlyDictionary<string, bool> Secrets);

/// <summary>What a save answers: the stored values, or the errors by wire name.</summary>
public sealed record SettingsGroupSaveResult(JsonObject? Values, IReadOnlyDictionary<string, bool>? Secrets, ValidationErrors Errors, bool Changed, bool Reloaded)
{
    public bool Succeeded => Errors.IsEmpty;
}

/// <summary>
/// Reads and writes settings groups (docs/MVC/parity.md › SettingsForm): the model's values keyed
/// by wire name, secrets never read out, a submission applied onto the stored model field by
/// field, checked by the model's rules (DataAnnotations and <c>Grove.Validation</c>) and the
/// group's own, saved only when it changed.
/// </summary>
public sealed class SettingsGroupService(IOptions<SettingsGroupOptions> options, IServiceProvider services)
{
    internal static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public IEnumerable<SettingsGroup> Groups => options.Value.Groups.Values;

    public SettingsGroup? Find(string groupId) => options.Value.Groups.TryGetValue(groupId, out var group) ? group : null;

    public IReadOnlyList<SettingsModel> ModelsOf(SettingsGroup group) => options.Value.ModelsOf(group.Id);

    public async Task<SettingsGroupForm> GetFormAsync(SettingsGroup group, CancellationToken cancellationToken = default)
    {
        var models = ModelsOf(group);
        var schema = await SettingsSchemaBuilder.BuildAsync(group, models, services, cancellationToken);
        var values = new JsonObject();
        var secrets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in models)
        {
            var members = SettingsSchemaBuilder.Members(model.ModelType);
            var instance = await model.Load(services, cancellationToken);
            var read = Read(instance, members);
            var prefix = models.Count == 1 ? string.Empty : model.Key;
            if (prefix.Length == 0)
            {
                values = read;
            }
            else
            {
                values[prefix] = read;
            }

            foreach (var (name, set) in SecretsOf(instance, members))
            {
                secrets[MemberKey.Join(prefix, name)] = set;
            }
        }

        return new SettingsGroupForm(schema, values, secrets);
    }

    public async Task<SettingsGroupSaveResult> SaveAsync(SettingsGroup group, JsonObject submitted, CancellationToken cancellationToken = default)
    {
        var models = ModelsOf(group);
        var errors = new ValidationErrors();
        var pending = new List<(SettingsModel Model, object Instance, string Prefix, bool Changed)>();

        foreach (var model in models)
        {
            var prefix = models.Count == 1 ? string.Empty : model.Key;
            var section = prefix.Length == 0 ? submitted : TryGet(submitted, prefix, out var node) ? node as JsonObject : null;
            var members = SettingsSchemaBuilder.Members(model.ModelType);
            var instance = await model.Load(services, cancellationToken);
            if (section is null)
            {
                pending.Add((model, instance, prefix, false));
                continue;
            }

            var before = JsonSerializer.Serialize(instance, model.ModelType, s_json);
            var modelErrors = new ValidationErrors();
            Apply(instance, members, section, string.Empty, modelErrors);
            if (modelErrors.IsEmpty)
            {
                Validate(instance, modelErrors);
            }

            if (modelErrors.IsEmpty && model.Validate is { } validate)
            {
                await validate(services, instance, modelErrors, cancellationToken);
            }

            foreach (var (key, messages) in modelErrors)
            {
                foreach (var message in messages)
                {
                    errors.Add(MemberKey.Join(prefix, key), message);
                }
            }

            pending.Add((model, instance, prefix, !string.Equals(before, JsonSerializer.Serialize(instance, model.ModelType, s_json), StringComparison.Ordinal)));
        }

        if (!errors.IsEmpty)
        {
            return new SettingsGroupSaveResult(null, null, errors, Changed: false, Reloaded: false);
        }

        var values = new JsonObject();
        var secrets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var reload = false;
        foreach (var (model, instance, prefix, changed) in pending)
        {
            if (changed)
            {
                await model.Save(services, instance, cancellationToken);
                if (model.Saved is { } saved)
                {
                    await saved(services, instance, cancellationToken);
                }

                reload |= model.ReloadsTenant;
            }

            var members = SettingsSchemaBuilder.Members(model.ModelType);
            var read = Read(instance, members);
            if (prefix.Length == 0)
            {
                values = read;
            }
            else
            {
                values[prefix] = read;
            }

            foreach (var (name, set) in SecretsOf(instance, members))
            {
                secrets[MemberKey.Join(prefix, name)] = set;
            }
        }

        if (reload)
        {
            services.GetService<IShellReleaseManager>()?.RequestRelease();
        }

        return new SettingsGroupSaveResult(values, secrets, errors, pending.Any(entry => entry.Changed), reload);
    }

    /// <summary>The model's values by wire name; secrets left out.</summary>
    public static JsonObject Read(object model, IReadOnlyList<SettingsMember> members)
    {
        var values = new JsonObject();
        foreach (var member in members)
        {
            if (member.Field.Secret)
            {
                continue;
            }

            values[member.Field.Name] = ToNode(member.Property.GetValue(model), member);
        }

        return values;
    }

    private static Dictionary<string, bool> SecretsOf(object model, IReadOnlyList<SettingsMember> members) =>
        members.Where(member => member.Field.Secret)
            .ToDictionary(member => member.Field.Name, member => member.Property.GetValue(model) is { } value && !string.IsNullOrEmpty(value.ToString()));

    private static JsonNode? ToNode(object? value, SettingsMember member)
    {
        if (value is null)
        {
            return null;
        }

        switch (member.Field.Kind)
        {
            case "object" when member.Children is { } children:
                return Read(value, children);
            case "objects" when member.Children is { } children && value is IEnumerable items:
                return new JsonArray([.. items.Cast<object?>().Select(item => item is null ? null : (JsonNode)Read(item, children))]);
        }

        if (value is string text && member.Setting?.Separator is { } separator)
        {
            return new JsonArray([.. Split(text, separator).Select(item => (JsonNode?)JsonValue.Create(item))]);
        }

        return value switch
        {
            Enum => JsonValue.Create(value.ToString()),
            Uri uri => JsonValue.Create(uri.OriginalString),
            _ => JsonSerializer.SerializeToNode(value, value.GetType(), s_json),
        };
    }

    /// <summary>Applies the submitted values onto <paramref name="model"/>; a value that does not convert is an error.</summary>
    internal void Apply(object model, IReadOnlyList<SettingsMember> members, JsonObject submitted, string prefix, ValidationErrors errors)
    {
        foreach (var member in members)
        {
            var field = member.Field;
            if (field.ReadOnly || !TryGet(submitted, field.Name, out var node))
            {
                continue;
            }

            var key = MemberKey.Join(prefix, field.Name);
            if (field.Secret)
            {
                var secret = node is JsonValue secretValue && secretValue.TryGetValue<string>(out var secretText) ? secretText : null;
                if (string.IsNullOrEmpty(secret))
                {
                    continue;
                }

                if (member.Setting?.ProtectedBy is { } purpose)
                {
                    secret = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose).Protect(secret);
                }

                member.Property.SetValue(model, secret);
                continue;
            }

            if (TryConvert(node, member, key, errors, out var value))
            {
                member.Property.SetValue(model, value);
            }
        }
    }

    private bool TryConvert(JsonNode? node, SettingsMember member, string key, ValidationErrors errors, out object? value)
    {
        var type = member.Property.PropertyType;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        value = null;

        try
        {
            if (node is null || (node is JsonValue empty && empty.TryGetValue<string>(out var emptyText) && emptyText.Length == 0 && target != typeof(string)))
            {
                value = type.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
                return true;
            }

            switch (member.Field.Kind)
            {
                case "object" when member.Children is { } children && node is JsonObject json:
                    var nested = Activator.CreateInstance(target)!;
                    Apply(nested, children, json, key, errors);
                    value = nested;
                    return true;

                case "objects" when member.Children is { } children && node is JsonArray array:
                    var itemType = SettingsSchemaBuilder.ItemType(target)!;
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                    for (var index = 0; index < array.Count; index++)
                    {
                        var item = Activator.CreateInstance(itemType)!;
                        if (array[index] is JsonObject itemJson)
                        {
                            Apply(item, children, itemJson, $"{key}[{index}]", errors);
                        }

                        list.Add(item);
                    }

                    value = Collection(list, target, itemType);
                    return true;
            }

            if (target == typeof(string) && member.Setting?.Separator is { } separator)
            {
                var items = node is JsonArray parts
                    ? parts.Select(part => part?.ToString()).Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim())
                    : Split(node.ToString(), separator);
                value = string.Join(separator, items);
                return true;
            }

            if (target.IsEnum)
            {
                if (Enum.TryParse(target, node.ToString(), ignoreCase: true, out var parsed) && Enum.IsDefined(target, parsed!))
                {
                    value = parsed;
                    return true;
                }

                errors.Add(key, string.Format(CultureInfo.CurrentCulture, "{0} is not one of the allowed values.", member.Field.Label));
                return false;
            }

            if (target == typeof(Uri))
            {
                var text = node.ToString().Trim();
                if (text.Length == 0)
                {
                    value = null;
                    return true;
                }

                if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
                {
                    value = uri;
                    return true;
                }

                errors.Add(key, string.Format(CultureInfo.CurrentCulture, "{0} is not a valid URL.", member.Field.Label));
                return false;
            }

            if (member.Field.Kind == "list" && node is JsonValue lines && lines.TryGetValue<string>(out var linesText))
            {
                var itemType = SettingsSchemaBuilder.ItemType(target) ?? typeof(string);
                var parsedLines = linesText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                value = Collection(parsedLines, target, itemType);
                return true;
            }

            if (member.Field.Kind == "json" && node is JsonValue jsonText && jsonText.TryGetValue<string>(out var raw))
            {
                node = string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw);
                if (node is null)
                {
                    value = null;
                    return true;
                }
            }

            value = node.Deserialize(type, s_json);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            errors.Add(key, string.Format(CultureInfo.CurrentCulture, "{0} is not valid.", member.Field.Label));
            return false;
        }
    }

    private static object Collection(IList list, Type target, Type itemType)
    {
        if (target.IsArray)
        {
            var array = Array.CreateInstance(itemType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        if (target.IsAssignableFrom(list.GetType()))
        {
            return list;
        }

        // A concrete collection type of its own (a HashSet, a custom list): built from the items.
        var typed = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        foreach (var item in list)
        {
            typed.Add(item);
        }

        return Activator.CreateInstance(target, typed)!;
    }

    private void Validate(object model, ValidationErrors errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model, services, null), results, validateAllProperties: true);
        errors.Add(results);
    }

    private static bool TryGet(JsonObject json, string name, out JsonNode? node)
    {
        foreach (var (key, value) in json)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                node = value;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static IEnumerable<string> Split(string text, string separator) =>
        text.Split(separator.Trim().Length == 0 ? [' ', '\t', '\n'] : [separator.Trim()[0], '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
