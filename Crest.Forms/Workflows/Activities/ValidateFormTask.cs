using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.ModelBinding;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;

namespace Crest.Forms.Workflows.Activities;

public class ValidateFormTask : TaskActivity<ValidateFormTask>
{
    private readonly IUpdateModelAccessor _updateModelAccessor;
    protected readonly IStringLocalizer S;

    public ValidateFormTask(
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizer<ValidateFormTask> localizer
    )
    {
        _updateModelAccessor = updateModelAccessor;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Validate Form Task"];

    public override LocalizedString Category => S["Validation"];

    public override bool HasEditor => false;

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Valid"], S["Invalid"]);

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var updater = _updateModelAccessor.ModelUpdater
            ?? throw new InvalidOperationException("Cannot add model validation errors when there's no Updater present.");

        var isValid = updater.ModelState.ErrorCount == 0;
        var outcome = isValid ? "Valid" : "Invalid";

        return Outcome(outcome);
    }
}
