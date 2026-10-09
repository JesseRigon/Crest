using Microsoft.AspNetCore.Http;
using OrchardCore.Templates.Models;
using OrchardCore.Templates.ViewModels;

namespace OrchardCore.Templates.Services;

/// <summary>
/// And instance of this class provides custom templates to use while previewing a page.
/// </summary>
public class PreviewTemplatesProvider
{
    private readonly Lazy<TemplatesDocument> _templatesDocument;

    public PreviewTemplatesProvider(IHttpContextAccessor httpContextAccessor)
    {
        _templatesDocument = new Lazy<TemplatesDocument>(() =>
        {
            var httpContext = httpContextAccessor.HttpContext;

            var templatesDocument = new TemplatesDocument();

            // No HttpContext outside a request (e.g. resolving the shape binding resolvers while
            // a tenant is activated in the background): nothing is being previewed then.
            if (httpContext is not null && httpContext.Items.TryGetValue("OrchardCore.PreviewTemplate", out var model))
            {
                var viewModel = model as TemplateViewModel;

                if (viewModel == null || viewModel.Name == null)
                {
                    return templatesDocument;
                }

                var template = new Template { Content = viewModel.Content };
                templatesDocument.Templates.Add(viewModel.Name, template);
            }

            return templatesDocument;
        });
    }

    public TemplatesDocument GetTemplates()
    {
        return _templatesDocument.Value;
    }
}
