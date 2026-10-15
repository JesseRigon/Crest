using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Localization;
using Grove.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.WorkflowProperties.Tabs.InputOutput.Models;
using FluentValidation;
using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.WorkflowProperties.Tabs.InputOutput.Validators;

/// <summary>
/// Represents the input model validator.
/// </summary>
public class InputModelValidator : AbstractValidator<InputDefinitionModel>
{
    public InputModelValidator(WorkflowDefinition workflowDefinition, ILocalizer localizer)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(localizer["Please enter a name for the input."]);
        
        RuleFor(x => x.Name)
            .Must((context, name, cancellationToken) =>
            {
                var existingInput = workflowDefinition.Inputs.FirstOrDefault(x => x.Name == name);
                return existingInput == null || existingInput.Name == context.Name;
            })
            .WithMessage(localizer["An input with this name already exists."]);
    }
}