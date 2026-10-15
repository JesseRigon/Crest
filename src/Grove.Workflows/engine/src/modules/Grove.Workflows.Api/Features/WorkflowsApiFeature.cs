using Grove.Workflows.Api.Constants;
using Grove.Workflows.Api.Endpoints;
using Grove.Workflows.Api.Endpoints.WorkflowInstances;
using Grove.Workflows.Api.Requirements;
using Grove.Workflows.Api.Serialization;
using Grove.Workflows.Api.Services;
using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Management.Features;
using Grove.Workflows.Runtime.Features;
using Grove.Workflows.SasTokens.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Api.Features;

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
