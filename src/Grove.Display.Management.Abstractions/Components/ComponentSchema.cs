using Microsoft.AspNetCore.Components;

namespace Grove.Display.Management.Components;

/// <summary>
/// How a <c>[Parameter]</c>'s CLR type maps onto the schema vocabulary (§ 1): its
/// <see cref="ParameterKind"/> and default editor, and whether it is a zone. The source
/// generator applies the same rules at build time; the reflection catalog applies them here,
/// so both produce the same descriptors.
/// </summary>
public static class ComponentSchema
{
    /// <summary>The editor kinds inferred from a CLR type. A <c>[ComponentParameter(Editor = ...)]</c>
    /// names any other editor component (picker, color, token, markup, ...).</summary>
    public static class Editors
    {
        public const string Text = "text";
        public const string Number = "number";
        public const string Bool = "bool";
        public const string Enum = "enum";
        public const string Date = "date";
        public const string Time = "time";
        public const string Action = "action";
        public const string Output = "output";
        public const string List = "list";
        public const string Object = "object";

        /// <summary>HTML: the server sanitizes the value before the tree leaves it (§ 10).</summary>
        public const string Markup = "markup";

        /// <summary>A media library path, picked from the library.</summary>
        public const string Media = "media";
    }

    /// <summary>A <c>RenderFragment</c> or <c>RenderFragment&lt;T&gt;</c> parameter is a zone, not a value.</summary>
    public static bool IsZone(Type type) =>
        type == typeof(RenderFragment) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RenderFragment<>));

    /// <summary>The kind, default editor and (for an enum) the choices of a parameter of <paramref name="type"/>.</summary>
    public static (ParameterKind Kind, string Editor, IReadOnlyList<string>? Choices) Infer(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) || type == typeof(Uri))
        {
            return (ParameterKind.String, Editors.Text, null);
        }

        if (type == typeof(bool))
        {
            return (ParameterKind.Boolean, Editors.Bool, null);
        }

        if (type.IsEnum)
        {
            return (ParameterKind.Choice, Editors.Enum, Enum.GetNames(type));
        }

        if (IsNumber(type))
        {
            return (ParameterKind.Number, Editors.Number, null);
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
        {
            return (ParameterKind.String, Editors.Date, null);
        }

        if (type == typeof(TimeOnly) || type == typeof(TimeSpan))
        {
            return (ParameterKind.String, Editors.Time, null);
        }

        if (type == typeof(EventCallback) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>)))
        {
            return (ParameterKind.EventHandler, Editors.Action, null);
        }

        if (type.IsArray || (type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && !IsDictionary(type)))
        {
            return (ParameterKind.Array, Editors.List, null);
        }

        return (ParameterKind.Object, Editors.Object, null);
    }

    /// <summary>The value an <c>EventCallback&lt;T&gt;</c> carries (its payload or output), else null.</summary>
    public static Type? EventValueType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>) ? type.GetGenericArguments()[0] : null;

    private static bool IsNumber(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
        || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    private static bool IsDictionary(Type type) =>
        typeof(System.Collections.IDictionary).IsAssignableFrom(type)
        || type.GetInterfaces().Append(type).Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IDictionary<,>)));

    /// <summary>The shell names of <c>[assembly: GroveShell]</c> ("admin", "site", "member").</summary>
    public static ShellKind? ParseShell(string? shell) => shell switch
    {
        "admin" => ShellKind.Admin,
        "site" => ShellKind.Site,
        "member" => ShellKind.Member,
        _ => null,
    };

    /// <summary>Every shell: what an assembly without a shell attribute serves.</summary>
    public static IReadOnlySet<ShellKind> AllShells { get; } = new HashSet<ShellKind> { ShellKind.Admin, ShellKind.Site, ShellKind.Member };
}
