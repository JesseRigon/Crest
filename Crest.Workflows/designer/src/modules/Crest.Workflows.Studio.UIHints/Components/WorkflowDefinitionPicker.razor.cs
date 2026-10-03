using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Requests;
using Crest.Workflows.Api.Client.Shared.Models;
using Crest.Workflows.Api.Client.Shared.UIHints.DropDown;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Components;

/// <summary>
/// Provides a component for picking a workflow definition.
/// </summary>
public partial class WorkflowDefinitionPicker
{
    private ICollection<SelectListItem> _items = Array.Empty<SelectListItem>();

    /// <summary>
    /// Gets or sets the editor context.
    /// </summary>
    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = null!;

    [Inject] private IWorkflowDefinitionService WorkflowDefinitionService { get; set; } = null!;


    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var request = new ListWorkflowDefinitionsRequest();
            var availableWorkflowDefinitions = await WorkflowDefinitionService.ListAsync(request, VersionOptions.Published);
            var workflowDefinitions = availableWorkflowDefinitions.Items;
            _items = workflowDefinitions.Select(x => new SelectListItem(x.Name, x.DefinitionId)).OrderBy(x => x.Text).ToList();

            StateHasChanged();
        }
    }

    private SelectListItem? GetSelectedValue()
    {
        var value = EditorContext.GetLiteralValueOrDefault();
        return _items.FirstOrDefault(x => x.Value == value);
    }

    private async Task OnValueChanged(SelectListItem? value)
    {
        await EditorContext.UpdateValueOrLiteralExpressionAsync(value?.Value ?? "");
    }
}