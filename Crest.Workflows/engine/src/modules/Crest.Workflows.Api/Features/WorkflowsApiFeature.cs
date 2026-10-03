using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.SasTokens.Features;
using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Api.Serialization;
using Crest.Workflows.Api.Services;
using Crest.Workflows.Management.Features;
using Crest.Workflows.Runtime.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Api.Features;

/// <summary>
/// Adds workflows API features.
/// </summary>
[DependsOn(typeof(WorkflowInstancesFeature))]
[DependsOn(typeof(WorkflowManagementFeature))]
[DependsOn(typeof(WorkflowRuntimeFeature))]
[DependsOn(typeof(SasTokensFeature))]
public class WorkflowsApiFeature(IModule module) : FeatureBase(module)
{
    /// <inheritdoc />
    public override void Configure()
    {
        Module.AddFastEndpointsAssembly(GetType());
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddSerializationOptionsConfigurator<SerializationConfigurator>();
        Module.AddFastEndpointsFromModule();

        Services.AddScoped<IWorkflowDefinitionLinker, StaticWorkflowDefinitionLinker>();
        Services.AddScoped<IAuthorizationHandler, NotReadOnlyRequirementHandler>();
        Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy(AuthorizationPolicies.NotReadOnlyPolicy, policy => policy.AddRequirements(new NotReadOnlyRequirement()));
        });
        Services.AddScoped<IWorkflowInstanceExportNameProvider, DefaultWorkflowInstanceExportNameProvider>();
    }
}