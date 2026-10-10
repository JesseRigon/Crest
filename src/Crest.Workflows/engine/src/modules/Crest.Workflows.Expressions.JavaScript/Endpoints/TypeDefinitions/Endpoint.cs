using System.Text;
using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Expressions.JavaScript.Endpoints.TypeDefinitions;

/// <summary>
/// Returns a TypeScript definition that is used by the Monaco editor to display intellisense for JavaScript expressions.
/// </summary>
[PublicAPI]
internal class Get : CrestWorkflowsEndpoint<Request>
{
    private readonly ITypeDefinitionService _typeDefinitionService;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;

    /// <inheritdoc />
    public Get(ITypeDefinitionService typeDefinitionService, IWorkflowDefinitionService workflowDefinitionService)
    {
        _typeDefinitionService = typeDefinitionService;
        _workflowDefinitionService = workflowDefinitionService;
    }
    
    /// <inheritdoc />
    public override void Configure()
    {
        Post("scripting/javascript/type-definitions/{workflowDefinitionId}");
        ConfigurePermissions("read:*", "read:javascript-type-definitions");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var workflowGraph = await GetWorkflowGraphAsync(request.WorkflowDefinitionId, cancellationToken);

        if (workflowGraph == null)
        {
            AddError($"Workflow definition {request.WorkflowDefinitionId} not found");
            await Send.ErrorsAsync(cancellation: cancellationToken);
            return;
        }
        
        var typeDefinitionContext = new TypeDefinitionContext(workflowGraph, request.ActivityTypeName, request.PropertyName, cancellationToken);
        var typeDefinitions = await _typeDefinitionService.GenerateTypeDefinitionsAsync(typeDefinitionContext);
        var fileName = $"crestWorkflows.{request.WorkflowDefinitionId}.d.ts";
        var data = Encoding.UTF8.GetBytes(typeDefinitions);

        await Send.BytesAsync(data, fileName, "application/x-typescript", cancellation: cancellationToken);
    }

    private async Task<WorkflowGraph?> GetWorkflowGraphAsync(string workflowDefinitionId, CancellationToken cancellationToken)
    {
        return await _workflowDefinitionService.FindWorkflowGraphAsync(workflowDefinitionId, VersionOptions.Latest, cancellationToken);
    }
}

internal record Request(string WorkflowDefinitionId, string? ActivityTypeName, string? PropertyName)
{
    public Request() : this(default!, default!, default)
    {
    }
}