using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Grove.Display.Management.Components;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Display.Templates.Services;

/// <summary>What a template evaluates to: its tree (a fragment when it has several top-level nodes) and what was dropped and why.</summary>
public sealed record TemplateEvaluation(DisplayNode Root, IReadOnlyList<DisplayDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Count > 0;
}

/// <summary>
/// Evaluates a template's Liquid into display-tree nodes (docs/blazor-display.md § 5, ruling
/// 2026-10-12). Each template is parsed on its own and cached by its source; a composite placed
/// by another template is evaluated separately with only its arguments in scope and linked into
/// the parent's tree, never concatenated as text (security audit item 1). Every evaluation runs
/// under the display limits: a step budget for the Liquid, a node budget and a deadline shared by
/// the whole page, and a composite depth; an overrun becomes an error node, never a retry.
/// Nothing is rendered here: free text becomes sanitized markup leaves, tags become nodes.
/// </summary>
public sealed class TemplateTreeEvaluator(
    TemplateTreeParser parser,
    IOptions<TemplateOptions> templateOptions,
    IOptions<DisplayTreeOptions> displayOptions,
    IServiceProvider services,
    IComponentRegistry registry,
    IMemoryCache memoryCache,
    ILogger<TemplateTreeEvaluator> logger)
{
    private TemplateBudget? _budget;
    private int _depth;

    /// <summary>Parses <paramref name="source"/> (cached by its hash); false with the parser's error.</summary>
    public bool TryParseTemplate(string source, out IFluidTemplate? template, out string? error)
    {
        var key = "Grove.Display.Templates.Parsed:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        if (memoryCache.TryGetValue<IFluidTemplate>(key, out template) && template is not null)
        {
            error = null;
            return true;
        }

        if (!parser.TryParse(source, out template, out error))
        {
            return false;
        }

        memoryCache.Set(key, template, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10), Size = source.Length });
        return true;
    }

    /// <summary>
    /// Evaluates <paramref name="source"/> with <paramref name="model"/> as <c>Model</c>; explicit
    /// node ids are scoped under <paramref name="instanceId"/>. Evaluations in the same request
    /// share one budget.
    /// </summary>
    public async Task<TemplateEvaluation> EvaluateAsync(
        string source,
        object? model,
        string instanceId,
        string? templateName = null,
        IEnumerable<KeyValuePair<string, FluidValue>>? values = null,
        CancellationToken cancellationToken = default)
    {
        // Every evaluation in this scope, composites included, shares the outermost one's budget.
        var budget = _budget ??= new TemplateBudget(displayOptions.Value, cancellationToken);
        _depth++;
        try
        {
            if (templateName is not null)
            {
                if (budget.Stack.Contains(templateName, StringComparer.OrdinalIgnoreCase))
                {
                    return Failed(instanceId, "cycle", $"The template '{templateName}' places itself: {string.Join(" → ", budget.Stack.Append(templateName))}.");
                }

                if (budget.Stack.Count >= budget.Options.MaxTemplateDepth)
                {
                    return Failed(instanceId, "limit", $"Templates nest deeper than {budget.Options.MaxTemplateDepth} levels.");
                }

                budget.Stack.Add(templateName);
            }

            return await EvaluateCoreAsync(source, model, instanceId, values, budget);
        }
        finally
        {
            if (templateName is not null && budget.Stack.Count > 0)
            {
                budget.Stack.RemoveAt(budget.Stack.Count - 1);
            }

            if (--_depth == 0)
            {
                _budget = null;
            }
        }
    }

    /// <summary>
    /// A composite placed by name: the published template, evaluated on its own with only the tag's
    /// arguments as its <c>Model</c>. A declared composite checks them against its inputs (unknown
    /// ones dropped, values converted to their types, defaults filled, required ones reported), and
    /// its declared outputs are re-published under the instance id, so <c>nodes.{id}.{output}</c>
    /// reads the inner node's output. A binding passed in stays a binding inside.
    /// </summary>
    internal async Task<DisplayNode?> EvaluateCompositeAsync(string name, string instanceId, IReadOnlyList<(string Name, FluidValue Value)> arguments, TemplateTreeState parent)
    {
        var resolver = services.GetRequiredService<TemplateResolver>();
        if (await resolver.ResolveAsync([name], displayType: null) is not { } template)
        {
            return null;
        }

        var model = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (template.IsComposite && template.Inputs.Count > 0)
        {
            foreach (var (argument, value) in arguments)
            {
                var input = template.Inputs.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, argument, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.Name, TemplateTreeTags.ToPascal(argument), StringComparison.OrdinalIgnoreCase));
                if (input is null)
                {
                    parent.Diagnose($"{instanceId}.{argument}", "dropped", $"'{argument}' is not an input of the composite '{template.Name}'.");
                    continue;
                }

                if (value is BindingValue)
                {
                    model[input.Name] = value;
                    continue;
                }

                if (input.Choices is { Length: > 0 } choices && !choices.Contains(value.ToStringValue(), StringComparer.OrdinalIgnoreCase))
                {
                    parent.Diagnose($"{instanceId}.{input.Name}", "dropped", $"'{input.Name}' must be one of {string.Join(", ", choices)}.");
                    continue;
                }

                var (_, _, type) = TemplateComposites.Kind(input.Type);
                if (!ComponentParameters.TryConvert(TemplateTreeState.ToJson(value), type, out var converted, out var error))
                {
                    parent.Diagnose($"{instanceId}.{input.Name}", "dropped", $"'{input.Name}' is not a valid {input.Type}: {error}");
                    continue;
                }

                model[input.Name] = converted;
            }

            foreach (var input in template.Inputs.Where(input => !model.ContainsKey(input.Name)))
            {
                if (input.Default is { } fallback)
                {
                    model[input.Name] = fallback.GetValueKind() switch
                    {
                        System.Text.Json.JsonValueKind.String => fallback.GetValue<string>(),
                        System.Text.Json.JsonValueKind.Number => System.Text.Json.JsonSerializer.Deserialize<decimal>(fallback),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        _ => fallback.DeepClone(),
                    };
                }
                else if (input.Required)
                {
                    parent.Diagnose($"{instanceId}.{input.Name}", "missing", $"The composite '{template.Name}' needs its input '{input.Name}'.");
                }
            }
        }
        else
        {
            foreach (var (argument, value) in arguments)
            {
                model[TemplateTreeTags.ToPascal(argument)] = value is BindingValue ? value : value.ToObjectValue();
            }
        }

        var evaluation = await EvaluateAsync(template.Source, model, instanceId, template.Name, cancellationToken: parent.Budget.CancellationToken);
        parent.Diagnostics.AddRange(evaluation.Diagnostics);
        if (template.Outputs.Count == 0)
        {
            return evaluation.Root;
        }

        // The composite's own node: its declared outputs read the inner nodes' (scoped under this instance).
        return new DisplayNode
        {
            Id = instanceId,
            Component = DisplayNode.FragmentComponent,
            Zones = new(StringComparer.OrdinalIgnoreCase) { ["ChildContent"] = [evaluation.Root] },
            OutputAliases = template.Outputs.ToDictionary(
                output => output.Name,
                output => DisplayBinding.FromOutput(instanceId + "." + output.Node, TemplateTreeTags.ToPascal(output.Output), output.Path),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    private async Task<TemplateEvaluation> EvaluateCoreAsync(string source, object? model, string instanceId, IEnumerable<KeyValuePair<string, FluidValue>>? values, TemplateBudget budget)
    {
        if (!TryParseTemplate(source, out var template, out var error))
        {
            return Failed(instanceId, "parse", error ?? "The template does not parse.");
        }

        var state = new TemplateTreeState(instanceId, registry, services.GetService<IHtmlSanitizerService>(), budget, services, this);
        var context = new LiquidTemplateContext(services, templateOptions.Value)
        {
            MaxSteps = budget.Options.MaxTemplateSteps,
        };

        context.AmbientValues[TemplateTreeState.AmbientKey] = state;
        context.SetValue("nodes", new NodesValue(state.Scope));
        context.SetValue("page", new PageVariablesValue());
        foreach (var (name, value) in values ?? [])
        {
            context.SetValue(name, value);
        }

        var writer = new TemplateTreeWriter(state);
        try
        {
            var remaining = budget.Remaining;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TemplateLimitException("No time is left for this template.");
            }

            var run = RenderAsync(template!, writer, context, model);

            await run.WaitAsync(remaining, budget.CancellationToken);
            state.FlushText();
        }
        catch (TimeoutException)
        {
            return Failed(instanceId, "limit", $"Evaluating the template took longer than {budget.Options.Timeout.TotalSeconds:0.###} s.", state.Diagnostics);
        }
        catch (TemplateLimitException exception)
        {
            return Failed(instanceId, "limit", exception.Message, state.Diagnostics);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("steps", StringComparison.OrdinalIgnoreCase))
        {
            return Failed(instanceId, "limit", $"The template ran more than {budget.Options.MaxTemplateSteps} statements.", state.Diagnostics);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            logger.LogWarning(exception, "The template evaluated as '{InstanceId}' failed.", instanceId);
            return Failed(instanceId, "error", exception.Message, state.Diagnostics);
        }

        var root = state.Roots.Count == 1
            ? state.Roots[0]
            : new DisplayNode
            {
                Id = instanceId,
                Component = DisplayNode.FragmentComponent,
                Zones = new(StringComparer.OrdinalIgnoreCase) { ["ChildContent"] = state.Roots },
            };

        return new TemplateEvaluation(root, state.Diagnostics);
    }

    private static async Task RenderAsync(IFluidTemplate template, TextWriter writer, LiquidTemplateContext context, object? model)
    {
        await context.InitializeAsync();

        if (model is not null)
        {
            context.SetValue("Model", model);
        }

        await template.RenderAsync(writer, HtmlEncoder.Default, context);
    }

    private static TemplateEvaluation Failed(string instanceId, string action, string reason, IEnumerable<DisplayDiagnostic>? earlier = null)
    {
        var diagnostic = new DisplayDiagnostic(instanceId, action, reason);
        return new TemplateEvaluation(DisplayNode.Error(instanceId, diagnostic), [.. earlier ?? [], diagnostic]);
    }
}
