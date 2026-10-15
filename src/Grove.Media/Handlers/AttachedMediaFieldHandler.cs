using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Media.Fields;
using Grove.Media.Services;

namespace Grove.Media.Handlers;

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
