using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.ModelBinding;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;

namespace Crest.Forms.Workflows.Activities;

// TODO: Add the ability to configure various types of validators.
public class ValidateFormFieldTask : TaskActivity<ValidateFormFieldTask>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    protected readonly IStringLocalizer S;

    public ValidateFormFieldTask(
        IHttpContextAccessor httpContextAccessor,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizer<ValidateFormFieldTask> localizer
    )
    {
        _httpContextAccessor = httpContextAccessor;
        _updateModelAccessor = updateModelAccessor;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Validate Form Field Task"];

    public override LocalizedString Category => S["Validation"];

    public string FieldName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string ErrorMessage
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Done"], S["Valid"], S["Invalid"]);

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var form = _httpContextAccessor.HttpContext.Request.Form;
        var fieldValue = form[FieldName];
        var isValid = !string.IsNullOrWhiteSpace(fieldValue);
        var outcome = isValid ? "Valid" : "Invalid";

        if (!isValid)
        {
            var updater = _updateModelAccessor.ModelUpdater;

            updater?.ModelState.TryAddModelError(FieldName, ErrorMessage);
        }

        return Outcome("Done", outcome);
    }
}
