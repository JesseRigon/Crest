using System.Text.Json;
using System.Text.Json.Serialization;
using Grove.Components.Modules;

namespace Grove.Routing;

/// <summary>
/// Which shell a page, an endpoint or a request belongs to: the shell's id, the same string
/// a client assembly declares with <see cref="GroveShellAttribute"/> ("admin", "site",
/// "member").
/// </summary>
/// <remarks>
/// An id rather than a closed enum, so a shell is a registration (<c>GroveShellDescriptor</c>)
/// and not a code change in every place that dispatches on the shell. Routing only ever asks
/// "is this the shell serving the request", never "which literal theme is active": a tenant's
/// admin, site and member themes are all active at once, so a theme-id comparison cannot
/// disambiguate a collision between shells (every shell declares <c>@page "/"</c>). See
/// <c>RouteGateMatcherPolicy</c>.
///
/// Site is the fallback: the shell of every request no registered shell claimed.
/// </remarks>
[JsonConverter(typeof(ShellIdJsonConverter))]
public readonly record struct ShellId(string Value) : IComparable<ShellId>
{
    /// <summary>Staff: the back office, at the tenant's admin prefix (and its login page).</summary>
    public static ShellId Admin { get; } = new(GroveShells.Admin);

    /// <summary>The public site, the fallback shell.</summary>
    public static ShellId Site { get; } = new(GroveShells.Site);

    /// <summary>Organization-bound users, at <see cref="MemberOptions.MemberUrlPrefix"/>.</summary>
    public static ShellId Member { get; } = new(GroveShells.Member);

    public int CompareTo(ShellId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value ?? string.Empty;
}

internal sealed class ShellIdJsonConverter : JsonConverter<ShellId>
{
    public override ShellId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new((reader.GetString() ?? GroveShells.Site).ToLowerInvariant());

    public override void Write(Utf8JsonWriter writer, ShellId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
