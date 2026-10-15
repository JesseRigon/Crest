using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Grove.Display.Management.Components;

/// <summary>
/// Values checked against a component's schema (§ 1: "the schema is shared between the server
/// (validation, defaults), the client renderer and the designer"). Conversion goes through JSON
/// with <see cref="JsonOptions"/>, the options <see cref="DisplayTreeRenderer"/> converts a
/// node's properties with, so a value that validates here renders there: a stored string
/// ("5", "H2", "true") converts to the parameter's CLR type, an enum by its name.
/// </summary>
public static class ComponentParameters
{
    /// <summary>The conversion options shared by validation and the renderer.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(), new BooleanFromStringConverter() },
    };

    // Stored parameters are strings (a part's Parameters, recipe JSON): "true" binds a bool the
    // way "5" binds an int under the web defaults' number handling.
    private sealed class BooleanFromStringConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String when bool.TryParse(reader.GetString(), out var value) => value,
            _ => throw new JsonException($"The JSON value could not be converted to {typeof(bool)}."),
        };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
    }

    /// <summary>Converts <paramref name="value"/> to <paramref name="type"/>.</summary>
    public static bool TryConvert(object? value, Type type, out object? converted, out string? error)
    {
        converted = null;
        error = null;

        if (value is null)
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
            {
                error = "null is not a value of a non-nullable type";
                return false;
            }

            return true;
        }

        if (type.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        try
        {
            var json = value as JsonNode ?? JsonSerializer.SerializeToNode(value, value.GetType(), JsonOptions);
            converted = json?.Deserialize(type, JsonOptions);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException or ArgumentException or FormatException)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// The values of <paramref name="values"/> that are parameters of <paramref name="component"/>,
    /// converted to their CLR types and keyed by the parameter's own name. A value that is not a
    /// parameter (a zone, an action, an unknown name) or does not convert is left out and
    /// reported in <paramref name="errors"/>, as is a required parameter without a value.
    /// </summary>
    public static Dictionary<string, object?> Bind(
        ComponentDescriptor component,
        IEnumerable<KeyValuePair<string, object?>> values,
        ICollection<string>? errors = null)
    {
        var bound = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            var parameter = component.Parameter(name);
            if (parameter is null || parameter.IsEvent)
            {
                errors?.Add($"'{name}' is not a parameter of '{component.Name}'.");
                continue;
            }

            var candidate = value;
            if (parameter.Choices is { Count: > 0 } choices && value is string text)
            {
                // The declared spelling: "h2" binds as "H2".
                candidate = choices.FirstOrDefault(choice => string.Equals(choice, text, StringComparison.OrdinalIgnoreCase));
                if (candidate is null)
                {
                    errors?.Add($"'{name}' must be one of {string.Join(", ", choices)}.");
                    continue;
                }
            }

            if (!TryConvert(candidate, parameter.ClrType, out var converted, out var error))
            {
                errors?.Add($"'{name}' is not a valid {parameter.ClrType.Name}: {error}");
                continue;
            }

            bound[parameter.Name] = converted;
        }

        foreach (var parameter in component.Parameters.Where(parameter => parameter.Required && !bound.ContainsKey(parameter.Name)))
        {
            errors?.Add($"'{parameter.Name}' is required.");
        }

        return bound;
    }
}
