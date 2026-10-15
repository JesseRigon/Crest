namespace Grove.Workflows.Studio.Models;

/// <summary>
/// Contains information about the server.
/// </summary>
/// <param name="PackageVersion">The installed package version of Grove.Workflows.</param>
public record ServerInformation(string PackageVersion);