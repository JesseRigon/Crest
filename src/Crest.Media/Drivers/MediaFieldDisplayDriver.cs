using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Media.Fields;
using Crest.Media.Library;
using Crest.Media.Services;
using Crest.Media.Settings;
using Crest.Media.ViewModels;
using Crest.Mvc.ModelBinding;

namespace Crest.Media.Drivers;

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
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

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

    /// <summary>
    /// The media picker works with library paths: each item is given its placement's current path
    /// (so a moved or renamed file shows where it is now) and its placement id, which the editor
    /// keeps when the path comes back unchanged.
    /// </summary>
    public override IDisplayResult Edit(MediaField field, BuildFieldEditorContext context)
    {
        return Initialize<EditMediaFieldViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = context.PartFieldDefinition.GetSettings<MediaFieldSettings>();
            var itemPaths = (await _references.ResolveAsync(field, new MediaUrlOptions(SignPrivate: false, IncludePath: true)))
                .Select(entry => new EditMediaFieldItemInfo
                {
                    Path = entry.Path ?? string.Empty,
                    PlacementId = entry.Resolved?.Item.Placement.Id ?? entry.Reference.PlacementId,
                })
                .ToArray();

            for (var i = 0; i < itemPaths.Length; i++)
            {
                if (settings.AllowMediaText && i < field.MediaTexts?.Length)
                {
                    itemPaths[i].MediaText = field.MediaTexts[i];
                }

                if (settings.AllowAnchors)
                {
                    var anchors = field.GetAnchors();
                    if (anchors != null && i < anchors.Length)
                    {
                        itemPaths[i].Anchor = anchors[i];
                    }
                }

                var filenames = field.GetAttachedFileNames();
                if (filenames != null && i < filenames.Length)
                {
                    itemPaths[i].AttachedFileName = filenames[i];
                }
            }

            model.Paths = JConvert.SerializeObject(itemPaths, JOptions.CamelCase);
            model.TempUploadFolder = _attachedMediaFieldFileService.GetMediaFieldsTempSubFolder();
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
            model.AllowMediaText = settings.AllowMediaText;
            model.AllowedExtensions = settings.AllowedExtensions ?? [];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(MediaField field, UpdateFieldEditorContext context)
    {
        var model = new EditMediaFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, f => f.Paths);

        // Deserializing an empty string doesn't return an array
        var items = string.IsNullOrWhiteSpace(model.Paths)
            ? []
            : JConvert.DeserializeObject<List<EditMediaFieldItemInfo>>(model.Paths, JOptions.CamelCase);

        // If it's an attached media field editor the files are automatically handled by _attachedMediaFieldFileService.
        if (string.Equals(context.PartFieldDefinition.Editor(), "Attached", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                field.SetAttachedFileNames(items.Where(i => !i.IsRemoved).Select(i => i.AttachedFileName).ToArray());
                await _attachedMediaFieldFileService.HandleFilesOnFieldUpdateAsync(items, context.ContentPart.ContentItem);
            }
            catch (AggregateException e)
            {
                e.Handle((x) =>
                {
                    if (x is FileNotFoundException ex)
                    {
                        // If the file is not found: add a model error specific for the field.
                        context.Updater.ModelState.AddModelError(
                            Prefix,
                            nameof(model.Paths),
                            S["{0}: The file {1} was not found. Reupload the file.", context.PartFieldDefinition.DisplayName(), ex.FileName]);
                        _logger.LogError(
                            e,
                            "{FileName} - {Message}, while handling attached media files for field '{Field}'",
                            ex.FileName,
                            ex.Message,
                            context.PartFieldDefinition.DisplayName());

                        return true;
                    }

                    return false;
                });
            }
            catch (Exception e)
            {
                // Log the error and add a generic error message.
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["{0}: There was an error handling the files.", context.PartFieldDefinition.DisplayName()]);
                _logger.LogError(e, "Error handling attached media files for field '{Field}'", context.PartFieldDefinition.DisplayName());
            }
        }

        // Ids are stored (ruling F1): an item keeps the placement it was opened with while its path
        // is unchanged; a new or changed path is resolved to its placement in the media library.
        // A path that resolves to nothing the editor may view is kept as a path only.
        var kept = items.Where(p => !p.IsRemoved).ToList();
        var previous = (await _references.ResolveAsync(field, new MediaUrlOptions(SignPrivate: false, IncludePath: true)))
            .Where(entry => entry.Resolved is not null && entry.Path is not null)
            .GroupBy(entry => MediaLibraryPaths.Normalize(entry.Path), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Resolved.Item.Placement.Id, StringComparer.Ordinal);

        var references = new List<MediaReference>(kept.Count);
        foreach (var item in kept)
        {
            var path = MediaLibraryPaths.Normalize(item.Path);
            if (previous.TryGetValue(path, out var placementId) &&
                (string.IsNullOrEmpty(item.PlacementId) || item.PlacementId == placementId))
            {
                references.Add(new MediaReference(placementId, item.Path));
                continue;
            }

            var resolved = !string.IsNullOrEmpty(item.PlacementId)
                ? await _references.ResolveAsync(MediaReference.ForPlacement(item.PlacementId), MediaUrlOptions.Unsigned)
                : null;
            resolved ??= string.IsNullOrEmpty(item.Path) ? null : await _references.ResolveAsync(MediaReference.ForPath(item.Path), MediaUrlOptions.Unsigned);
            references.Add(new MediaReference(resolved?.Item.Placement.Id, item.Path));
        }

        field.SetReferences(references);

        var settings = context.PartFieldDefinition.GetSettings<MediaFieldSettings>();

        if (settings.AllowedExtensions?.Length > 0)
        {
            for (var i = 0; i < field.Paths.Length; i++)
            {
                if (string.IsNullOrEmpty(field.Paths[i]))
                {
                    continue;
                }

                var extension = Path.GetExtension(field.Paths[i]);

                if (!settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["Media extension is not allowed. Only media with '{0}' extensions are allowed.", string.Join(", ", settings.AllowedExtensions)]);
                }
            }
        }

        if (settings.Required && field.Paths.Length < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }

        if (field.Paths.Length > 1 && !settings.Multiple)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["{0}: Selecting multiple media is forbidden.", context.PartFieldDefinition.DisplayName()]);
        }

        if (settings.AllowMediaText)
        {
            field.MediaTexts = items.Select(t => t.MediaText).ToArray();
        }
        else
        {
            field.MediaTexts = [];
        }

        if (settings.AllowAnchors)
        {
            field.SetAnchors(items.Select(t => t.Anchor).ToArray());
        }
        else if (field.Content.ContainsKey("Anchors")) // Less well known properties should be self healing.
        {
            field.Content.Remove("Anchors");
        }

        return Edit(field, context);
    }
}
