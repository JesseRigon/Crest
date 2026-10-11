using Crest.Media.Library;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.Media.Processing;
using Crest.Media.Fields;

namespace Crest.Media.GraphQL;

public class MediaFieldQueryObjectType : ObjectGraphType<MediaField>
{
    public MediaFieldQueryObjectType(IStringLocalizer<MediaFieldQueryObjectType> S)
    {
        Name = nameof(MediaField);

        if (MediaAppContextSwitches.EnableLegacyMediaFields)
        {
            Field<ListGraphType<StringGraphType>, IEnumerable<string>>("paths")
                .Description(S["the media paths"])
                .PagingArguments()
                .Resolve(x =>
                {
                    if (x.Source?.Paths is null)
                    {
                        return Array.Empty<string>();
                    }

                    return x.Page(x.Source.Paths);
                });

            Field<ListGraphType<StringGraphType>, IEnumerable<string>>("fileNames")
                .Description(S["the media file names"])
                .PagingArguments()
                .Resolve(x =>
                {
                    var fileNames = x.Page(x.Source.GetAttachedFileNames());
                    if (fileNames is null)
                    {
                        return Array.Empty<string>();
                    }
                    return fileNames;
                });

            Field<ListGraphType<StringGraphType>, IEnumerable<string>>("urls")
                .Description(S["the urls of the media items the caller may view"])
                .PagingArguments()
                .ResolveAsync(async x =>
                {
                    var references = x.RequestServices.GetRequiredService<IMediaReferenceResolver>();
                    var entries = await references.ResolveAsync(x.Source);
                    return x.Page(entries).Select(entry => entry.Resolved?.Url ?? string.Empty).ToArray();
                });

            Field<ListGraphType<StringGraphType>, IEnumerable<string>>("mediatexts")
            .Description(S["the media texts"])
            .PagingArguments()
            .Resolve(x =>
            {
                if (x.Source?.MediaTexts is null)
                {
                    return Array.Empty<string>();
                }
                return x.Page(x.Source.MediaTexts);
            });
        }

        Field<ListGraphType<StringGraphType>, IEnumerable<string>>("placementIds")
            .Description(S["the placement ids of the media items (empty for an item stored only by path)"])
            .PagingArguments()
            .Resolve(x => x.Source is null ? [] : x.Page(x.Source.GetReferences()).Select(reference => reference.PlacementId ?? string.Empty).ToArray());

        // The items the caller may view, resolved through the access check (ruling F1): ids
        // first, paths for content not yet migrated.
        Field<ListGraphType<MediaFileItemType>, IEnumerable<MediaFileItem>>("files")
            .Description(S["the files of the media items"])
            .PagingArguments()
            .ResolveAsync(async x =>
            {
                if (x.Source is null)
                {
                    return [];
                }

                var references = x.RequestServices.GetRequiredService<IMediaReferenceResolver>();
                var entries = x.Page(await references.ResolveAsync(x.Source)).ToArray();
                var fileNames = x.Source.GetAttachedFileNames();
                var mediaTexts = x.Source.MediaTexts ?? [];
                return entries
                    .Where(entry => entry.Resolved is not null)
                    .Select(entry => new MediaFileItem
                    {
                        PlacementId = entry.Resolved.Item.Placement.Id,
                        Path = entry.Path ?? string.Empty,
                        FileName = fileNames.Length > entry.Index && !string.IsNullOrEmpty(fileNames[entry.Index]) ? fileNames[entry.Index] : entry.Name,
                        Url = entry.Resolved.Url,
                        MediaText = mediaTexts.Length > entry.Index ? mediaTexts[entry.Index] : string.Empty,
                    })
                    .ToArray();
            });
    }
}

public sealed class MediaFileItemType : ObjectGraphType<MediaFileItem>
{
    public MediaFileItemType(IStringLocalizer<MediaFileItemType> S)
    {
        Field<StringGraphType>("fileName")
            .Description(S["the file name of the media file item"])
            .Resolve(x => x.Source.FileName);

        Field<StringGraphType>("placementId")
            .Description(S["the placement id of the media file item"])
            .Resolve(x => x.Source.PlacementId);

        Field<StringGraphType>("path")
            .Description(S["the media library path of the media file item, when it has one"])
            .Resolve(x => x.Source.Path);

        Field<StringGraphType>("url")
            .Description(S["the url name of the media file item"])
            .Resolve(x => x.Source.Url);

        Field<StringGraphType>("resizeUrl")
            .Description(S["the url of the media file item with image processing parameters"])
            .Argument<StringGraphType>("profile", arg => arg.Description = S["the media profile name to use for image processing"])
            .Argument<IntGraphType>("width", arg => arg.Description = S["the width of the image"])
            .Argument<IntGraphType>("height", arg => arg.Description = S["the height of the image"])
            .Argument<StringGraphType>("mode", arg => arg.Description = S["the resize mode (pad, boxpad, max, min, stretch)"])
            .Argument<IntGraphType>("quality", arg => arg.Description = S["the quality of the image (1-100)"])
            .Argument<StringGraphType>("format", arg => arg.Description = S["the format of the image (jpg, png, gif, webp)"])
            .Argument<StringGraphType>("bgcolor", arg => arg.Description = S["the background color of the image"])
            .Argument<BooleanGraphType>("autoorient", arg => arg.Description = S["auto-orient the image based on EXIF data (defaults to true)"])
            .ResolveAsync(async x =>
            {
                if (string.IsNullOrEmpty(x.Source.Url))
                {
                    return x.Source.Url;
                }

                var profile = x.GetArgument<string>("profile");
                var width = x.GetArgument<int?>("width");
                var height = x.GetArgument<int?>("height");
                var mode = x.GetArgument<string>("mode");
                var quality = x.GetArgument<int?>("quality");
                var format = x.GetArgument<string>("format");
                var bgcolor = x.GetArgument<string>("bgcolor");
                var autoorient = x.GetArgument<bool?>("autoorient");

                var platformHelper = x.RequestServices.GetRequiredService<IPlatformHelper>();

                var resizeMode = !string.IsNullOrEmpty(mode) && Enum.TryParse<ResizeMode>(mode, ignoreCase: true, out var parsed)
                    ? parsed
                    : ResizeMode.Undefined;

                var imageFormat = !string.IsNullOrEmpty(format) && Enum.TryParse<Format>(format, ignoreCase: true, out var parsedFormat)
                    ? parsedFormat
                    : Format.Undefined;

                // The resolved /files URL; resizing runs there after the access check (ruling F2).
                string assetUrl;
                if (!string.IsNullOrEmpty(profile))
                {
                    assetUrl = await platformHelper.ImageProfileResizeUrlAsync(
                        x.Source.Url,
                        profile,
                        width,
                        height,
                        resizeMode,
                        quality,
                        imageFormat,
                        bgcolor: bgcolor,
                        autoorient: autoorient);
                }
                else
                {
                    assetUrl = platformHelper.ImageResizeUrl(
                        x.Source.Url,
                        width,
                        height,
                        resizeMode,
                        quality,
                        imageFormat,
                        bgcolor: bgcolor,
                        autoorient: autoorient);
                }

                // The value is already url-encoded
                return assetUrl;
            });

        Field<StringGraphType>("mediaText")
            .Description(S["the media text of the file item"])
            .Resolve(x => x.Source.MediaText);
    }
}

public sealed class MediaFileItem
{
    public string PlacementId { get; set; }
    public string FileName { get; set; }
    public string Path { get; set; }
    public string Url { get; set; }
    public string MediaText { get; set; }
}
