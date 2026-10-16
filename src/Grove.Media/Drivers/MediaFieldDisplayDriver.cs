using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Media.Fields;
using Grove.Media.Library;
using Grove.Media.Services;
using Grove.Media.Settings;
using Grove.Media.ViewModels;

namespace Grove.Media.Drivers;

public sealed class MediaFieldDisplayDriver : ContentFieldDisplayDriver<MediaField>
{
    private readonly AttachedMediaFieldFileService _attachedMediaFieldFileService;
    private readonly IMediaReferenceResolver _references;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public MediaFieldDisplayDriver(AttachedMediaFieldFileService attachedMediaFieldFileService,
        IMediaReferenceResolver references,
        IStringLocalizer<MediaFieldDisplayDriver> localizer,
        ILogger<MediaFieldDisplayDriver> logger)
    {
        _attachedMediaFieldFileService = attachedMediaFieldFileService;
        _references = references;
        S = localizer;
        _logger = logger;
    }

    /// <summary>
    /// The items the viewer may see (ruling F1): each placement id, or the path of content not
    /// yet migrated, opened through the access check, with its <c>/files</c> URL.
    /// </summary>
    public override IDisplayResult Display(MediaField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayMediaFieldViewModel>(GetDisplayShapeType(context), async model =>
        {
            model.Field = field;
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;

            var anchors = field.GetAnchors();
            model.Items = [.. (await _references.ResolveAsync(field))
                .Where(entry => entry.Resolved is not null)
                .Select(entry => new MediaFieldDisplayItem
                {
                    PlacementId = entry.Resolved.Item.Placement.Id,
                    Path = entry.Path,
                    Url = entry.Resolved.Url,
                    Name = entry.Name,
                    IsImage = entry.IsImage,
                    IsPublic = entry.Resolved.IsPublic,
                    MediaText = field.MediaTexts?.ElementAtOrDefault(entry.Index) ?? string.Empty,
                    Anchor = anchors.ElementAtOrDefault(entry.Index),
                })];
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
