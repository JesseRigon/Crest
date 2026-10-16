using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Data.Types;

/// <summary>
/// One value type (docs/blazor-display.md, rulings 2026-10-09, module homes; queries.md): the one
/// place a value type is declared with what every system needs to handle it. The field types
/// that store it, the editor component that edits it and the display primitive that shows it
/// (component registry names), the column type a query returns it as, and the type a workflow
/// input or output carries it as. A module that owns a type (Money) registers it here.
/// </summary>
public sealed record ValueTypeDescriptor(
    string Name,
    string QueryColumnType,
    string WorkflowType,
    IReadOnlyList<string> FieldTypes,
    string? Editor = null,
    string? Display = null,
    Type? ClrType = null,
    string? DisplayName = null,
    string? Module = null);

/// <summary>The column and workflow types the base value types use (queries' typed columns, the UI action contract's types).</summary>
public static class ValueTypeKinds
{
    public const string String = "string";
    public const string Number = "number";
    public const string Boolean = "boolean";
    public const string Date = "date";
    public const string DateTime = "datetime";
    public const string Object = "object";
    public const string Array = "array";
}

/// <summary>The value types Grove ships, over the content fields every tenant has.</summary>
public static class ValueTypes
{
    public static IReadOnlyList<ValueTypeDescriptor> Base { get; } =
    [
        new("Text", ValueTypeKinds.String, ValueTypeKinds.String, ["TextField"], "TextFieldEditor", "Label", typeof(string), "Text"),
        new("Number", ValueTypeKinds.Number, ValueTypeKinds.Number, ["NumericField"], "NumericFieldEditor", "Label", typeof(decimal), "Number"),
        new("Boolean", ValueTypeKinds.Boolean, ValueTypeKinds.Boolean, ["BooleanField"], "BooleanFieldEditor", "Label", typeof(bool), "Yes/no"),
        new("Date", ValueTypeKinds.Date, ValueTypeKinds.String, ["DateField"], "DateFieldEditor", "Label", typeof(DateOnly), "Date"),
        new("DateTime", ValueTypeKinds.DateTime, ValueTypeKinds.String, ["DateTimeField"], "DateFieldEditor", "Label", typeof(System.DateTime), "Date and time"),
        new("Html", ValueTypeKinds.String, ValueTypeKinds.String, ["HtmlField"], "HtmlEditor", "RichText", typeof(string), "Rich text"),
        new("Markdown", ValueTypeKinds.String, ValueTypeKinds.String, ["MarkdownField"], "MarkdownEditor", "MarkdownBody", typeof(string), "Markdown"),
        new("Link", ValueTypeKinds.Object, ValueTypeKinds.Object, ["LinkField"], "LinkFieldEditor", "Link", null, "Link"),
        new("Options", ValueTypeKinds.Array, ValueTypeKinds.Array, ["OptionListField"], "OptionListFieldEditor", null, typeof(string[]), "Options"),
        new("Permalink", ValueTypeKinds.String, ValueTypeKinds.String, [], "TextFieldEditor", "Link", typeof(string), "Permalink"),
    ];
}

/// <summary>The registry every system reads value types from.</summary>
public interface IValueTypeRegistry
{
    IReadOnlyCollection<ValueTypeDescriptor> Types { get; }

    bool TryGet(string name, out ValueTypeDescriptor valueType);

    /// <summary>The value type a content field or part type stores.</summary>
    bool TryGetByField(string fieldType, out ValueTypeDescriptor valueType);
}

/// <summary>What modules registered (<see cref="ValueTypeServiceCollectionExtensions.AddValueType"/>).</summary>
public sealed class ValueTypeOptions
{
    public List<ValueTypeDescriptor> Types { get; } = [];
}

/// <summary>The base types, then the registered ones; a registration of the same name replaces the earlier one.</summary>
public sealed class ValueTypeRegistry : IValueTypeRegistry
{
    private readonly Dictionary<string, ValueTypeDescriptor> _byName = new(StringComparer.OrdinalIgnoreCase);

    public ValueTypeRegistry(IOptions<ValueTypeOptions> options)
        : this(options.Value.Types)
    {
    }

    public ValueTypeRegistry(IEnumerable<ValueTypeDescriptor> registered)
    {
        foreach (var valueType in ValueTypes.Base.Concat(registered))
        {
            _byName[valueType.Name] = valueType;
        }
    }

    public IReadOnlyCollection<ValueTypeDescriptor> Types => _byName.Values;

    public bool TryGet(string name, out ValueTypeDescriptor valueType) => _byName.TryGetValue(name, out valueType!);

    public bool TryGetByField(string fieldType, out ValueTypeDescriptor valueType)
    {
        valueType = _byName.Values.LastOrDefault(candidate => candidate.FieldTypes.Contains(fieldType, StringComparer.OrdinalIgnoreCase))!;
        return valueType is not null;
    }
}

public static class ValueTypeServiceCollectionExtensions
{
    /// <summary>Registers the value type registry (once) and, when given, a module's value type.</summary>
    public static IServiceCollection AddValueType(this IServiceCollection services, ValueTypeDescriptor? valueType = null)
    {
        services.AddOptions<ValueTypeOptions>();
        if (!services.Any(service => service.ServiceType == typeof(IValueTypeRegistry)))
        {
            services.AddSingleton<IValueTypeRegistry, ValueTypeRegistry>();
        }

        if (valueType is not null)
        {
            services.Configure<ValueTypeOptions>(options => options.Types.Add(valueType));
        }

        return services;
    }
}
