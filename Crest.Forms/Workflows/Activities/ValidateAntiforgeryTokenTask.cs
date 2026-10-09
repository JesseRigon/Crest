using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;

namespace Crest.Forms.Workflows.Activities;

public class ValidateAntiforgeryTokenTask : TaskActivity<ValidateAntiforgeryTokenTask>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAntiforgery _antiforgery;
    protected readonly IStringLocalizer S;

    public ValidateAntiforgeryTokenTask(
        IHttpContextAccessor httpContextAccessor,
        IAntiforgery antiforgery,
        IStringLocalizer<ValidateAntiforgeryTokenTask> localizer
    )
    {
        _httpContextAccessor = httpContextAccessor;
        _antiforgery = antiforgery;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Validate Antiforgery Token Task"];

    public override LocalizedString Category => S["Validation"];

    public override bool HasEditor => false;

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Done"], S["Valid"], S["Invalid"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (await _antiforgery.IsRequestValidAsync(_httpContextAccessor.HttpContext))
        {
            return Outcome("Done", "Valid");
        }
        else
        {
            return Outcome("Done", "Invalid");
        }
    }
}
