using Grove.Workflows.Common.Services;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Common.Features;

/// <summary>
/// Configures the system clock.
/// </summary>
public class SystemClockFeature : FeatureBase
{
    /// <inheritdoc />
    public SystemClockFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddSingleton<ISystemClock, SystemClock>();
    }
}