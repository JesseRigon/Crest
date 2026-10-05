using Crest.Routing;

namespace Crest.Themes;

/// <summary>
/// What a module needs from a shell's active theme for its pages to work.
/// </summary>
/// <remarks>
/// A module states this once ("my admin pages need a crest-blazor admin theme"), and the
/// check resolves it against the active theme of that bucket and its <c>BaseTheme</c>
/// ancestors.
///
/// Per module rather than per contributed page set: a module's pages are written against
/// one shell contract in practice, and per-page granularity would multiply the
/// declarations without changing any answer.
/// </remarks>
public sealed record ShellContract(RouteBucket Bucket, bool RequiresCrestBlazor = true);

/// <summary>
/// Declares the shell contracts a feature's pages depend on.
/// </summary>
/// <remarks>
/// Implemented by a module and resolved from the shell's services, so Crest learns what a
/// module needs without referencing it - the same direction every other Crest seam runs.
/// A module that contributes no Blazor pages implements nothing and is never gated.
/// </remarks>
public interface IShellContractProvider
{
    /// <summary>The feature these contracts belong to, as Orchard knows it.</summary>
    string FeatureId { get; }

    /// <summary>The shell contracts this feature's pages require.</summary>
    IEnumerable<ShellContract> GetContracts();
}

/// <summary>One feature whose shell contract the active themes do not satisfy.</summary>
public sealed record ShellIncompatibility(
    string FeatureId,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<RouteBucket>))]
    RouteBucket Bucket,
    string? ActiveThemeId,
    string Reason);

/// <summary>
/// What an API returns when shell contracts are not satisfied.
/// </summary>
/// <remarks>
/// Carried in the response body rather than signalled only by the status code, so a
/// scripted or recipe-driven caller gets the same actionable report a person does.
/// </remarks>
public sealed record ShellIncompatibilityReport(
    string Message,
    IReadOnlyList<ShellIncompatibility> Incompatibilities);
