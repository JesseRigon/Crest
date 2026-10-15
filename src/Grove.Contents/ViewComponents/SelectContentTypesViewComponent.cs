using Microsoft.AspNetCore.Mvc;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.Contents.ViewModels;

namespace Grove.Contents.ViewComponents;

public class SelectContentTypesViewComponent : ViewComponent
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SelectContentTypesViewComponent(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IViewComponentResult> InvokeAsync(IEnumerable<string> selectedContentTypes, string htmlName, string stereotype)
    {
        var contentTypes = await ContentTypeSelection.BuildAsync(_contentDefinitionManager, selectedContentTypes ?? []);

        if (!string.IsNullOrEmpty(stereotype))
        {
            contentTypes = contentTypes
                .Where(x => x.ContentTypeDefinition.StereotypeEquals(stereotype))
                .ToArray();
        }

        var model = new SelectContentTypesViewModel
        {
            HtmlName = htmlName,
            ContentTypeSelections = contentTypes,
        };

        return View(model);
    }
}
