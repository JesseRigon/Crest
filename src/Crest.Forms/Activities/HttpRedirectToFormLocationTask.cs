using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Crest.Workflows.Platform;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;

namespace Crest.Forms.Activities;

public class HttpRedirectToFormLocationTask : TaskActivity<HttpRedirectToFormLocationTask>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly IStringLocalizer S;

    public HttpRedirectToFormLocationTask(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<HttpRedirectToFormLocationTask> stringLocalizer
    )
    {
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["Http Redirect To Form Location Task"];

    public override LocalizedString Category => S["HTTP"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Done"], S["Failed"]);

    public string FormLocationKey
    {
        get => GetProperty(() => string.Empty);
        set => SetProperty(value ?? string.Empty);
    }

    public override Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (workflowContext.Output.TryGetValue(WorkflowConstants.HttpFormLocationOutputKeyName, out var obj)
            && obj is Dictionary<string, string> formLocations)
        {
            if (formLocations.TryGetValue(FormLocationKey, out var path))
            {
                _httpContextAccessor.HttpContext.Items[WorkflowConstants.FormOriginatedLocationItemsKey] = path;

                return Task.FromResult(Outcome("Done"));
            }
        }

        return Task.FromResult(Outcome("Failed"));
    }
}
