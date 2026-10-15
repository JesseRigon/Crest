#nullable enable

using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;
using Grove.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Infrastructure.Settings.Groups;

/// <summary>A settings group's form, as the settings form renders it.</summary>
public sealed record SettingsSchema(
    string GroupId,
    string Caption,
    string? Description,
    bool ReloadsTenant,
    IReadOnlyList<SettingsField> Fields);

/// <summary>
/// One field of a settings form: its wire name (the key of its value and of its errors), its
/// editor, its section and the rules the browser can show before the server checks them all.
/// </summary>
public sealed record SettingsField
{
    public required string Name { get; init; }

    public required string Label { get; init; }

    /// <summary>
    /// <c>text</c>, <c>textarea</c>, <c>password</c>, <c>email</c>, <c>url</c>, <c>integer</c>,
    /// <c>number</c>, <c>boolean</c>, <c>select</c>, <c>multiselect</c>, <c>list</c>, <c>color</c>,
    /// <c>code</c>, <c>json</c>, <c>object</c>, <c>objects</c>.
    /// </summary>
    public required string Kind { get; init; }

    public string? Description { get; init; }

    public string? Section { get; init; }

    public string? Placeholder { get; init; }

    public bool Required { get; init; }

    public bool ReadOnly { get; init; }

    /// <summary>Write-only: never in the values; <c>secrets</c> says whether one is stored.</summary>
    public bool Secret { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public int? MaxLength { get; init; }

    public string? Language { get; init; }

    /// <summary>Shown only when another field matches: <c>{ field, values }</c> (no values: truthy).</summary>
    public SettingsFieldCondition? VisibleWhen { get; init; }

    public IReadOnlyList<SettingOption>? Options { get; init; }

    /// <summary>The fields of an <c>object</c>, or of each item of <c>objects</c>.</summary>
    public IReadOnlyList<SettingsField>? Fields { get; init; }
}

/// <summary>The condition under which a field is shown.</summary>
public sealed record SettingsFieldCondition(string Field, IReadOnlyList<string> Values);

/// <summary>
/// Derives a settings form from its model type, once per type: every public read/write property
/// that is not <c>[ScaffoldColumn(false)]</c> or <c>[JsonIgnore]</c>, labelled, sectioned and ordered
/// by <c>[Display]</c>, its editor from the property type, <c>[DataType]</c> and
/// <see cref="SettingAttribute"/>, its hints from the validation rules.
/// </summary>
public static class SettingsSchemaBuilder
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<SettingsMember>> s_members = new();

    /// <summary>The members of a model type, in form order.</summary>
    public static IReadOnlyList<SettingsMember> Members(Type type) => s_members.GetOrAdd(type, Describe);

    /// <summary>
    /// The group's schema, with each field's options resolved from <paramref name="services"/>: the
    /// model's fields for a group of one model, else one section (an <c>object</c> field under the
    /// model's key) per model.
    /// </summary>
    public static async Task<SettingsSchema> BuildAsync(SettingsGroup group, IReadOnlyList<SettingsModel> models, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var reloads = models.Any(model => model.ReloadsTenant);
        if (models.Count == 1)
        {
            return new(group.Id, group.Caption, group.Description, reloads, await FieldsAsync(Members(models[0].ModelType), services, cancellationToken));
        }

        var sections = new List<SettingsField>(models.Count);
        foreach (var model in models)
        {
            sections.Add(new SettingsField
            {
                Name = model.Key,
                Label = model.Caption ?? group.Caption,
                Kind = "object",
                Fields = await FieldsAsync(Members(model.ModelType), services, cancellationToken),
            });
        }

        return new(group.Id, group.Caption, group.Description, reloads, sections);
    }

    private static async Task<IReadOnlyList<SettingsField>> FieldsAsync(IReadOnlyList<SettingsMember> members, IServiceProvider services, CancellationToken cancellationToken)
    {
        var fields = new List<SettingsField>(members.Count);
        foreach (var member in members)
        {
            var field = member.Field;
            if (member.Setting?.Options is { } optionsType)
            {
                var source = (ISettingOptions)ActivatorUtilities.GetServiceOrCreateInstance(services, optionsType);
                field = field with { Options = await source.GetOptionsAsync(cancellationToken) };
            }

            if (member.Children is { } children)
            {
                field = field with { Fields = await FieldsAsync(children, services, cancellationToken) };
            }

            fields.Add(field);
        }

        return fields;
    }

    private static IReadOnlyList<SettingsMember> Describe(Type type)
    {
        var members = new List<(SettingsMember Member, int Order, int Index)>();
        var index = 0;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0
                || property.GetCustomAttribute<ScaffoldColumnAttribute>() is { Scaffold: false }
                || property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
            {
                continue;
            }

            var display = property.GetCustomAttribute<DisplayAttribute>();
            members.Add((Describe(property, display), display?.GetOrder() ?? 10_000, index++));
        }

        return [.. members.OrderBy(member => member.Order).ThenBy(member => member.Index).Select(member => member.Member)];
    }

    private static SettingsMember Describe(PropertyInfo property, DisplayAttribute? display)
    {
        var setting = property.GetCustomAttribute<SettingAttribute>();
        var dataType = property.GetCustomAttribute<DataTypeAttribute>()?.DataType;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var itemType = ItemType(type);
        IReadOnlyList<SettingsMember>? children = null;

        var kind = setting?.Editor;
        if (setting?.Secret == true)
        {
            kind ??= "password";
        }
        else if (kind is null && setting?.Options is not null)
        {
            kind = itemType is not null || (type == typeof(string) && setting.Separator is not null) ? "multiselect" : "select";
        }
        else if (kind is null)
        {
            kind = type switch
            {
                _ when type == typeof(bool) => "boolean",
                _ when type.IsEnum => "select",
                _ when IsInteger(type) => "integer",
                _ when type == typeof(double) || type == typeof(float) || type == typeof(decimal) => "number",
                _ when type == typeof(Uri) => "url",
                _ when type == typeof(string) && setting?.Separator is not null => setting.Options is null ? "list" : "multiselect",
                _ when type == typeof(string) => setting?.Options is not null ? "select" : dataType switch
                {
                    DataType.MultilineText => "textarea",
                    DataType.Password => "password",
                    DataType.EmailAddress => "email",
                    DataType.Url or DataType.ImageUrl => "url",
                    DataType.Html => "code",
                    _ => "text",
                },
                _ when itemType == typeof(string) => setting?.Options is not null ? "multiselect" : "list",
                _ when itemType is not null && IsObject(itemType) => "objects",
                _ when itemType is null && IsObject(type) => "object",
                _ => "json",
            };
        }

        if (kind == "object" && !type.IsAbstract)
        {
            children = Members(type);
        }
        else if (kind == "objects" && itemType is not null && !itemType.IsAbstract)
        {
            children = Members(itemType);
        }

        var rules = property.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
        var (min, max) = Bounds(rules);
        var field = new SettingsField
        {
            Name = MemberKey.Of(property),
            Label = display?.GetName() ?? Words(property.Name),
            Kind = kind,
            Description = display?.GetDescription(),
            Section = display?.GetGroupName(),
            Placeholder = setting?.Placeholder ?? display?.GetPrompt(),
            Required = rules.Any(rule => rule is RequiredAttribute or NotEmptyAttribute),
            ReadOnly = setting?.ReadOnly == true || property.GetCustomAttribute<EditableAttribute>() is { AllowEdit: false },
            Secret = setting?.Secret == true,
            Min = min,
            Max = max,
            MaxLength = rules.Select(MaxLengthOf).FirstOrDefault(length => length is not null),
            Language = setting?.Language ?? (kind == "json" ? "json" : dataType == DataType.Html ? "html" : null),
            VisibleWhen = Condition(setting?.VisibleWhen, property.DeclaringType!),
            Options = type.IsEnum ? EnumOptions(type) : null,
        };

        return new SettingsMember(property, field, setting, children);
    }

    internal static Type? ItemType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable is null || typeof(IDictionary).IsAssignableFrom(type)
            || type.GetInterfaces().Any(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
        {
            return null;
        }

        return enumerable.GetGenericArguments()[0];
    }

    private static bool IsInteger(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort);

    private static bool IsObject(Type type) =>
        type.IsClass && type != typeof(string) && type != typeof(Uri) && !typeof(IEnumerable).IsAssignableFrom(type)
        && type.GetConstructor(Type.EmptyTypes) is not null;

    private static (double? Min, double? Max) Bounds(IEnumerable<ValidationAttribute> rules)
    {
        double? min = null;
        double? max = null;
        foreach (var rule in rules)
        {
            switch (rule)
            {
                case RangeAttribute range when range.Minimum is IConvertible && range.Maximum is IConvertible:
                    min = Convert.ToDouble(range.Minimum, System.Globalization.CultureInfo.InvariantCulture);
                    max = Convert.ToDouble(range.Maximum, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case InclusiveBetweenAttribute between:
                    (min, max) = (between.From, between.To);
                    break;
                case GreaterThanOrEqualAttribute { Value: { } value }:
                    min = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case LessThanOrEqualAttribute { Value: { } value }:
                    max = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
            }
        }

        return (min, max);
    }

    private static int? MaxLengthOf(ValidationAttribute rule) => rule switch
    {
        MaxLengthAttribute length when length.Length > 0 => length.Length,
        StringLengthAttribute length => length.MaximumLength,
        MaximumLengthAttribute length => length.Maximum,
        LengthBetweenAttribute length => length.Maximum,
        _ => null,
    };

    private static SettingsFieldCondition? Condition(string? expression, Type declaringType)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var equals = expression.IndexOf('=');
        var name = (equals < 0 ? expression : expression[..equals]).Trim();
        var property = declaringType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var key = property is null ? name : MemberKey.Of(property);
        var values = equals < 0 ? [] : expression[(equals + 1)..].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new SettingsFieldCondition(key, values);
    }

    private static IReadOnlyList<SettingOption> EnumOptions(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => new SettingOption(field.Name, field.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? Words(field.Name)))];

    /// <summary>"RetentionDays" → "Retention days".</summary>
    internal static string Words(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 && char.IsUpper(character) && (char.IsLower(name[index - 1]) || (index + 1 < name.Length && char.IsLower(name[index + 1]))))
            {
                builder.Append(' ');
                builder.Append(index + 1 < name.Length && char.IsUpper(name[index + 1]) ? character : char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}

/// <summary>A model property and the field derived from it.</summary>
public sealed record SettingsMember(PropertyInfo Property, SettingsField Field, SettingAttribute? Setting, IReadOnlyList<SettingsMember>? Children);
