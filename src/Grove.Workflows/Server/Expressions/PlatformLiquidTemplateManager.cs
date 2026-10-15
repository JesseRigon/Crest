using System.Text.Encodings.Web;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Extensions;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Options;
using EngineLiquid = Grove.Workflows.Expressions.Liquid.Contracts.ILiquidTemplateManager;
using PlatformLiquid = Grove.Liquid.ILiquidTemplateManager;

namespace Grove.Workflows.Expressions;

/// <summary>
/// The engine's Liquid, run by the platform's (docs/blazor-display.md § 12, step 2): a workflow's
/// Liquid expression is parsed and rendered by the platform's template manager (one Fluid, the
/// platform's cached parser, tags, filters and member allow-list), with the workflow's values as
/// properties: <c>Variables</c> (the variables in scope), <c>Input</c> (the activity's input over
/// the workflow's), <c>CorrelationId</c>, <c>WorkflowDefinitionId</c>,
/// <c>WorkflowDefinitionVersionId</c>, <c>WorkflowDefinitionVersion</c> and <c>WorkflowInstanceId</c>.
/// Replaces the engine's own template manager, so the engine's Liquid handler renders through it.
/// </summary>
public sealed class PlatformLiquidTemplateManager(PlatformLiquid platform, IOptions<TemplateOptions> options) : EngineLiquid
{
    public async Task<string?> RenderAsync(string template, ExpressionExecutionContext expressionExecutionContext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return default;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await platform.RenderStringAsync(template, NullEncoder.Default, model: null, Properties(expressionExecutionContext, options.Value));
    }

    public bool Validate(string template, out string error)
    {
        var valid = platform.Validate(template, out var errors);
        error = string.Join(System.Environment.NewLine, errors ?? []);
        return valid;
    }

    private static IEnumerable<KeyValuePair<string, FluidValue>> Properties(ExpressionExecutionContext context, TemplateOptions options)
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variable in context.EnumerateVariablesInScope())
        {
            if (!variables.ContainsKey(variable.Name) && variable.TryGet(context, out _))
            {
                variables[variable.Name] = variable.Get(context);
            }
        }

        var input = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (context.TryGetWorkflowExecutionContext(out var workflow))
        {
            foreach (var (name, value) in workflow.Input)
            {
                input[name] = value;
            }
        }

        if (context.TryGetActivityExecutionContext(out var activity))
        {
            foreach (var (name, value) in activity.ActivityInput)
            {
                input[name] = value;
            }
        }

        yield return new("Variables", FluidValue.Create(variables, options));
        yield return new("Input", FluidValue.Create(input, options));

        if (workflow is not null)
        {
            yield return new("CorrelationId", FluidValue.Create(workflow.CorrelationId, options));
            yield return new("WorkflowDefinitionId", FluidValue.Create(workflow.Workflow.Identity.DefinitionId, options));
            yield return new("WorkflowDefinitionVersionId", FluidValue.Create(workflow.Workflow.Identity.Id, options));
            yield return new("WorkflowDefinitionVersion", FluidValue.Create(workflow.Workflow.Identity.Version, options));
            yield return new("WorkflowInstanceId", FluidValue.Create(workflow.Id, options));
        }
    }
}
