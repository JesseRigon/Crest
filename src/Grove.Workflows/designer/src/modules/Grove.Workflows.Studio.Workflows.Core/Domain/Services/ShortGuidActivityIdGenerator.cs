using DEDrake;
using Grove.Workflows.Studio.Workflows.Domain.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// Generates unique activity IDs using <see cref="ShortGuid"/>.
/// </summary>
public class ShortGuidIdentityGenerator : IIdentityGenerator
{
    /// <inheritdoc />
    public string GenerateId() => ShortGuid.NewGuid().ToString();
}