using DEDrake;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// Generates unique activity IDs using <see cref="ShortGuid"/>.
/// </summary>
public class ShortGuidIdentityGenerator : IIdentityGenerator
{
    /// <inheritdoc />
    public string GenerateId() => ShortGuid.NewGuid().ToString();
}