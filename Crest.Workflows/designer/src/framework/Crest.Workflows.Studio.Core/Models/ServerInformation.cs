namespace Crest.Workflows.Studio.Models;

/// <summary>
/// Contains information about the server.
/// </summary>
/// <param name="PackageVersion">The installed package version of Crest.Workflows.</param>
public record ServerInformation(string PackageVersion);