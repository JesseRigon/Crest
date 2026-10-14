using Crest.DisplayManagement.Components;

namespace Crest.Templates.Services;

/// <summary>
/// Checks a template before it is saved (§ 5: validated on save; security audit item 7: the
/// palette is enforced on the server on every path): it parses, every tag names a registered
/// component or a published composite, every argument fits the schema, and the wiring of § 4a
/// is sound, and each workflow action names a workflow that offers a UI action with exactly the
/// arguments it passes. It is evaluated without a model, so what depends on data is checked when it
/// runs.
/// </summary>
public sealed class TemplateValidator(TemplateTreeEvaluator evaluator, IComponentRegistry registry, IEnumerable<IDisplayActionValidator> actionValidators)
{
    public async Task<IReadOnlyList<DisplayDiagnostic>> ValidateAsync(string name, string source, CancellationToken cancellationToken = default) =>
        await ValidateAsync(new Models.TemplatePart { Name = name, Source = source }, cancellationToken);

    /// <summary>A template with its composite declaration: inputs are sampled into the model, outputs must name inner outputs.</summary>
    public async Task<IReadOnlyList<DisplayDiagnostic>> ValidateAsync(Models.TemplatePart template, CancellationToken cancellationToken = default)
    {
        var name = template.Name;
        var source = template.Source;
        if (string.IsNullOrWhiteSpace(name))
        {
            return [new(string.Empty, "invalid", "A template needs the shape or composite name it binds.")];
        }

        if (!evaluator.TryParseTemplate(source ?? string.Empty, out _, out var error))
        {
            return [new(name, "parse", error ?? "The template does not parse.")];
        }

        // A composite is evaluated with its inputs' defaults (or a sample of their type) as its model.
        var model = template.IsComposite
            ? template.Inputs.ToDictionary(
                input => input.Name,
                input => (object?)(input.Default?.DeepClone() ?? (input.Type?.ToLowerInvariant() switch
                {
                    "number" => 0m,
                    "boolean" => false,
                    _ => (object?)(input.Choices is { Length: > 0 } choices ? choices[0] : string.Empty),
                })),
                StringComparer.OrdinalIgnoreCase)
            : null;

        var evaluation = await evaluator.EvaluateAsync(source!, model, instanceId: "validate", templateName: name, cancellationToken: cancellationToken);
        var diagnostics = new List<DisplayDiagnostic>([.. evaluation.Diagnostics.Where(diagnostic => diagnostic.Action != "missing"), .. DisplayTreeValidator.Validate(evaluation.Root, registry)]);

        // Server actions are checked by the module that owns them (a workflow's UI action contract).
        foreach (var node in DisplayTreeValidator.Walk(evaluation.Root))
        {
            foreach (var (eventName, actions) in node.Events ?? [])
            {
                foreach (var action in actions.Where(action => !action.IsClientOnly))
                {
                    foreach (var validator in actionValidators)
                    {
                        foreach (var problem in await validator.ValidateAsync(action, cancellationToken))
                        {
                            diagnostics.Add(new($"{node.Id}.{eventName}", "action", problem));
                        }
                    }
                }
            }
        }

        if (template.IsComposite)
        {
            var nodes = DisplayTreeValidator.Walk(evaluation.Root).ToDictionary(node => node.Id, StringComparer.Ordinal);
            foreach (var output in template.Outputs)
            {
                var inner = nodes.GetValueOrDefault("validate." + output.Node);
                var outputName = TemplateTreeTags.ToPascal(output.Output);
                if (inner is null)
                {
                    diagnostics.Add(new(name, "output", $"The output '{output.Name}' names the node '{output.Node}', which the template does not place."));
                }
                else if (inner.OutputAliases?.ContainsKey(outputName) != true
                    && (!registry.TryGet(inner.Component, out var descriptor) || descriptor.Parameter(outputName) is not { Kind: ParameterKind.Output }))
                {
                    diagnostics.Add(new(name, "output", $"'{output.Output}' is not an output of '{inner.Component}' (the node '{output.Node}')."));
                }
            }

            foreach (var input in template.Inputs.Where(input => string.IsNullOrWhiteSpace(input.Name)))
            {
                diagnostics.Add(new(name, "input", "Every input needs a name."));
            }
        }

        return diagnostics;
    }
}
