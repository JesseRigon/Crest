using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Shapes;
using Crest.DisplayManagement;
using Crest.DisplayManagement.ModelBinding;
using Crest.Flows.Models;
using Crest.Flows.ViewModels;

namespace Crest.Flows.Controllers;

[Admin("Flows/{action}", "Flows.{action}")]
public sealed class AdminController : Controller
{
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IShapeFactory _shapeFactory;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    public AdminController(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IShapeFactory shapeFactory,
        IUpdateModelAccessor updateModelAccessor)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _shapeFactory = shapeFactory;
        _updateModelAccessor = updateModelAccessor;
    }

    public async Task<IActionResult> BuildEditor(string id, string prefix, string prefixesName, string contentTypesName, string contentItemsName, string targetId, bool flowMetadata, string parentContentType, string partName, string cardCollectionType = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var contentItem = await _contentManager.NewAsync(id);
        IEnumerable<ContentTypeDefinition> containedContentTypes = null;
        cardCollectionType ??= nameof(BagPart);

        // Create a Card Shape
        var contentCard = await _shapeFactory.CreateAsync<ContentCardShape>("ContentCard", shape =>
        {
            // Updater is the controller for AJAX Requests
            shape.Updater = _updateModelAccessor.ModelUpdater;
            // Shape Specific
            shape.CollectionShapeType = cardCollectionType;
            shape.ContentItem = contentItem;
            shape.BuildEditor = true;
            shape.ParentContentType = parentContentType;
            shape.CollectionPartName = partName;
            shape.ContainedContentTypes = containedContentTypes;
            // Card Specific Properties
            shape.TargetId = targetId;
            shape.Inline = true;
            shape.CanMove = true;
            shape.CanDelete = true;
            // Input hidden
            // Prefixes
            shape.PrefixValue = prefix;
            shape.PrefixesName = prefixesName;
            // ContentTypes
            shape.ContentTypesName = contentTypesName;
            // ContentItems
            shape.ContentItemsName = contentItemsName;
        });

        var model = new BuildEditorViewModel
        {
            EditorShape = contentCard,
        };
        return View("Display", model);
    }
}
