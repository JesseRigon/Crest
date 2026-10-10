namespace Crest.Workflows.Studio.Models;

/// <summary>
/// Contains information about the client.
/// </summary>
/// <param name="PackageVersion">The installed package version of Crest.Workflows Studio.</param>
public record ClientInformation(string PackageVersion);