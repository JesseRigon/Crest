using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;
using Crest.Media.Fields;
using Crest.Media.Services;

namespace Crest.Media.Handlers;

public class AttachedMediaFieldHandler : ContentFieldHandler<MediaField>
{
    private readonly AttachedMediaFieldFileService _attachedMediaFieldFileService;

    public AttachedMediaFieldHandler(AttachedMediaFieldFileService attachedMediaFieldFileService)
    {
        _attachedMediaFieldFileService = attachedMediaFieldFileService;
    }

    public async override Task ClonedAsync(CloneContentFieldContext context, MediaField field)
    {
        if (context.ContentPartFieldDefinition.Editor() == "Attached")
        {
            var mediaFieldNodePath = (field.Content.Node as JsonNode).GetPath();

            var updatedPaths = await _attachedMediaFieldFileService.CopyFilesAsync(field.Paths, context.CloneContentItem);

            var mediaFieldNode = (context.CloneContentItem.Content as JsonDynamicObject).SelectNode(mediaFieldNodePath);

            // Update the paths to the new ones.
            mediaFieldNode[nameof(MediaField.Paths)] = JArray.FromObject(updatedPaths);
        }
    }
}
