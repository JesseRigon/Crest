using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.ModelBinding;
using Crest.ReCaptcha.Services;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;

namespace Crest.ReCaptcha.Workflows;

public class ValidateReCaptchaTask : TaskActivity<ValidateReCaptchaTask>
{
    private readonly ReCaptchaService _reCaptchaService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    protected readonly IStringLocalizer S;

    public ValidateReCaptchaTask(
        ReCaptchaService reCaptchaService,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizer<ValidateReCaptchaTask> localizer
    )
    {
        _reCaptchaService = reCaptchaService;
        _updateModelAccessor = updateModelAccessor;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Validate ReCaptcha Task"];

    public override LocalizedString Category => S["Validation"];

    public override bool HasEditor => false;

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Done"], S["Valid"], S["Invalid"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var outcome = "Valid";

        await _reCaptchaService.ValidateCaptchaAsync((key, error) =>
        {
            var updater = _updateModelAccessor.ModelUpdater;
            outcome = "Invalid";

            updater?.ModelState.TryAddModelError(Constants.ReCaptchaServerResponseHeaderName, S["Captcha validation failed. Try again."]);
        });

        return Outcome("Done", outcome);
    }
}
