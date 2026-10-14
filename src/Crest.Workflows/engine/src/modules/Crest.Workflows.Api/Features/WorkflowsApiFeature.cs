using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Endpoints;
using Crest.Workflows.Api.Endpoints.WorkflowInstances;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Api.Serialization;
using Crest.Workflows.Api.Services;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Management.Features;
using Crest.Workflows.Runtime.Features;
using Crest.Workflows.SasTokens.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Api.Features;

/// <summary>
/// The management API's services: its serializer configuration, the per-endpoint policies
/// (<see cref="EngineApiPolicies"/>), the read-only guard and the export helpers. The host maps
/// the endpoints themselves with <see cref="EngineApiEndpoints.MapEngineApi"/>.
/// </summary>
[DependsOn(typeof(WorkflowInstancesFeature))]
[DependsOn(typeof(WorkflowManagementFeature))]
[DependsOn(typeof(WorkflowRuntimeFeature))]
[DependsOn(typeof(SasTokensFeature))]
public class WorkflowsApiFeature(IModule module) : FeatureBase(module)
{
    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddSerializationOptionsConfigurator<SerializationConfigurator>();

        Services.AddScoped<IWorkflowDefinitionLinker, StaticWorkflowDefinitionLinker>();
        Services.AddScoped<IAuthorizationHandler, NotReadOnlyRequirementHandler>();
        Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy(AuthorizationPolicies.NotReadOnlyPolicy, policy => policy.AddRequirements(new NotReadOnlyRequirement()));
            EngineApiPolicies.Register(options);
        });
        Services.AddScoped<IWorkflowInstanceExportNameProvider, DefaultWorkflowInstanceExportNameProvider>();
        Services.AddScoped<WorkflowInstanceExportEndpoints.InstanceExporter>();
        Services.AddScoped<WorkflowInstanceExportEndpoints.InstanceImporter>();
    }
}
