using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.Services;
using Microsoft.JSInterop;

namespace Crest.Workflows.Studio.Monaco.Handlers;

/// <summary>
/// Handles Monaco editor for JavaScript.
/// </summary>
public class JavaScriptMonacoHandler(IJSRuntime jsRuntime, TypeDefinitionService typeDefinitionService)
    : IMonacoHandler
{
    /// <inheritdoc />
    public async ValueTask InitializeAsync(MonacoContext context)
    {
        if (context.ExpressionDescriptor.Type != "JavaScript")
            return;

        var activityDescriptor = context.CustomProperties.TryGetValue(nameof(ActivityDescriptor), out var activityDescriptorObj) ? (ActivityDescriptor)activityDescriptorObj : null;
        var propertyDescriptor = context.CustomProperties.TryGetValue(nameof(PropertyDescriptor), out var propertyDescriptorObj) ? (PropertyDescriptor)propertyDescriptorObj : null;
        var workflowDefinitionId = context.CustomProperties.TryGetValue("WorkflowDefinitionId", out var workflowDefinitionIdObj) ? (string)workflowDefinitionIdObj : null;
        var activityTypeName = activityDescriptor?.TypeName;
        var propertyName = propertyDescriptor?.Name;

        if (activityTypeName == null || workflowDefinitionId == null)
            return;

        var data = await typeDefinitionService.GetTypeDefinition(workflowDefinitionId, activityTypeName, propertyName);
        await jsRuntime.InvokeVoidAsync("monaco.languages.typescript.javascriptDefaults.addExtraLib", data, null);
    }
}