using Crest.DisplayManagement.Components;

namespace Crest.Templates.Services;

/// <summary>
/// <see cref="ITemplateHtmlRenderer"/> over the template chain: the tenant's published template of
/// the name, else a shipped one (<see cref="IShippedTemplateProvider"/>), evaluated with the model
/// and rendered to HTML. No viewer filter: the caller (an email) chose the recipient and the
/// model, and the tree has no data reads of its own beyond what the template names.
/// </summary>
public sealed class TemplateHtmlRenderer(TemplateResolver resolver, TemplateTreeEvaluator evaluator, IDisplayHtmlRenderer html) : ITemplateHtmlRenderer
{
    public async Task<string?> RenderAsync(string templateName, object model, CancellationToken cancellationToken = default)
    {
        if (await resolver.ResolveAsync([templateName], displayType: null) is not { } template)
        {
            return null;
        }

        var evaluation = await evaluator.EvaluateAsync(template.Source, model, instanceId: templateName, templateName, cancellationToken: cancellationToken);
        return await html.RenderAsync(evaluation.Root, cancellationToken);
    }
}
