using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Common.Features;

/// <summary>
/// Adds and configures the Mediator feature.
/// </summary>
public class MediatorFeature : FeatureBase
{
    /// <inheritdoc />
    public MediatorFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddMediator()
            .AddMediatorHostedServices();
    }
}