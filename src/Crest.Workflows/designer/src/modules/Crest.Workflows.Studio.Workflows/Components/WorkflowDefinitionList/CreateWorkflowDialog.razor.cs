using Blazored.FluentValidation;
using Crest.Workflows.Studio.Localization;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;
using Crest.Workflows.Studio.Workflows.Models;
using Crest.Workflows.Studio.Workflows.Validators;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Crest.Workflows.Studio.Workflows.Components.WorkflowDefinitionList;

/// <summary>
/// A dialog that allows the user to create a new workflow.
/// </summary>
public partial class CreateWorkflowDialog
{
    private readonly WorkflowMetadataModel _metadataModel = new();
    private EditContext _editContext = null!;
    private WorkflowPropertiesModelValidator _validator = null!;
    private FluentValidationValidator _fluentValidationValidator = null!;
   
    /// <summary>
    /// The name of the workflow to create.
    /// </summary>
    [Parameter] public string WorkflowName { get; set; } = "New workflow";
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Inject] private IWorkflowDefinitionService WorkflowDefinitionService { get; set; } = null!;    

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _metadataModel.Name = WorkflowName;
        _editContext = new(_metadataModel);
        _validator = new(WorkflowDefinitionService, Localizer);
    }

    private Task OnCancelClicked()
    {
        MudDialog.Cancel();
        return Task.CompletedTask;
    }

    private async Task OnSubmitClicked()
    {
        if(!await _fluentValidationValidator.ValidateAsync())
            return;

        await OnValidSubmit();
    }

    private async Task OnValidSubmit()
    {
        var result = await WorkflowDefinitionService.CreateNewDefinitionAsync(_metadataModel.Name!, _metadataModel.Description!);
        MudDialog.Close(result);
    }
}