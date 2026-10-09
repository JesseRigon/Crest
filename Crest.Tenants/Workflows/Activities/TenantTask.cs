using Microsoft.Extensions.Localization;
using Crest.Environment.Shell;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Services;

namespace Crest.Tenants.Workflows.Activities;

public abstract class TenantTask : TenantActivity, ITask
{
    protected TenantTask(IShellSettingsManager shellSettingsManager, IShellHost shellHost, IWorkflowExpressionEvaluator expressionEvaluator, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer localizer)
        : base(shellSettingsManager, shellHost, expressionEvaluator, scriptEvaluator, localizer)
    {
    }
}
