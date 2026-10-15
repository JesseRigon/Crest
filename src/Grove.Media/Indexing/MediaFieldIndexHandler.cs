using Grove.Data.Storage;
using Grove.Media.Library;
using Microsoft.Extensions.Options;
using Grove.Contents.Indexing;
using Grove.Environment.Shell.Builders;
using Grove.Data.Indexing;
using Grove.Media.Fields;
using Grove.Media.Settings;

namespace Grove.Media.Indexing;

public class MediaFieldIndexHandler : ContentFieldIndexHandler<MediaField>
{
    private const string MediaTextKeySuffix = ".MediaText";
    private const string FileTextKeySuffix = ".FileText";

    private readonly IMediaFileStore _mediaFileStore;
    private readonly IMediaReferenceResolver _references;
    private readonly MediaFileIndexingOptions _mediaFileIndexingOptions;
    private readonly IServiceProvider _serviceProvider;

    public MediaFieldIndexHandler(
        IMediaFileStore mediaFileStore,
        IMediaReferenceResolver references,
        IOptions<MediaFileIndexingOptions> mediaFileIndexingOptions,
        IServiceProvider serviceProvider)
    {
        _mediaFileStore = mediaFileStore;
        _references = references;
        _mediaFileIndexingOptions = mediaFileIndexingOptions.Value;
        _serviceProvider = serviceProvider;
    }

    public override async Task BuildIndexAsync(MediaField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();
        var settings = context.ContentPartFieldDefinition.GetSettings<MediaFieldSettings>();

        var references = field.GetReferences();
        if (references.Count == 0)
        {
            foreach (var key in context.Keys)
            {
                context.DocumentIndex.Set(key + MediaTextKeySuffix, ContentIndexingConstants.NullValue, options);
                context.DocumentIndex.Set(key + FileTextKeySuffix, ContentIndexingConstants.NullValue, options);
            }

            return;
        }

        if (settings.AllowMediaText)
        {
            foreach (var key in context.Keys)
            {
                if (field.MediaTexts != null)
                {
                    foreach (var mediaText in field.MediaTexts)
                    {
                        context.DocumentIndex.Set(key + MediaTextKeySuffix, mediaText, options);
                    }
                }
                else
                {
                    context.DocumentIndex.Set(key + MediaTextKeySuffix, ContentIndexingConstants.NullValue, options);
                }
            }
        }

        // Each item by its placement id, or its path for content not yet migrated (ruling F1),
        // read through the access check as the indexing caller.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            // The same file could be added several times to the field.
            if (reference.IsEmpty || !seen.Add(reference.PlacementId ?? reference.Path))
            {
                continue;
            }

            var resolved = await _references.ResolveAsync(reference, MediaUrlOptions.Unsigned);
            var name = resolved?.Item.Placement.Name ?? reference.Path;
            var providerType = _mediaFileIndexingOptions.GetRegisteredMediaFileTextProvider(Path.GetExtension(name ?? string.Empty));
            if (providerType == null)
            {
                continue;
            }

            using var fileStream = resolved is not null
                ? await _references.OpenReadAsync(reference)
                : await LegacyStreamAsync(reference.Path);

            if (fileStream == null)
            {
                continue;
            }

            var fileText = await _serviceProvider.CreateInstance<IMediaFileTextProvider>(providerType)
                .GetTextAsync(name, fileStream);

            foreach (var key in context.Keys)
            {
                context.DocumentIndex.Set(key + FileTextKeySuffix, fileText, options);
            }
        }
    }

    /// <summary>Before the library is active, the media folder on disk.</summary>
    private async Task<Stream> LegacyStreamAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return await _mediaFileStore.GetFileStreamAsync(path);
        }
        catch (FileStoreException)
        {
            return null;
        }
    }
}
