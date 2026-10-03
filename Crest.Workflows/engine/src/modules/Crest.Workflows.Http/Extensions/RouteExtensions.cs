// ReSharper disable once CheckNamespace

namespace Crest.Workflows.Extensions;

/// <summary>
/// Provides extension methods for strings that represent a route.
/// </summary>
public static class RouteExtensions
{
    /// <summary>
    /// Normalizes a route by ensuring a leading slash, removing any trailing slash.
    /// </summary>
    public static string NormalizeRoute(this string path) => $"/{path.Trim('/')}";
}