using Crest.Workflows.Common.Services;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Common.Features;

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