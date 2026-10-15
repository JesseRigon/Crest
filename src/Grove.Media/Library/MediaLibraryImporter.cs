#nullable enable
using Grove.FileStorage;
using Grove.Media.Files;
using Grove.Modules;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DriveType = Grove.Media.Files.DriveType;

namespace Grove.Media.Library;

/// <summary>
/// The one-time move of the <c>/media</c> tree into file objects (ruling F1), safe to run again:
/// <list type="number">
/// <item>creates the tenant's "Media library" shared drive if it has none;</item>
/// <item>imports the media folder on disk: folders become folder objects, files become file
/// objects whose first version is the existing bytes, through the quarantine (see
/// <see cref="ImportAsync"/>); anything already in the drive under the same name is kept;</item>
/// <item>moves the folder rights to sharing (<see cref="MediaLibrarySharingImport"/>);</item>
/// <item>hands <c>/media</c> to the drive (the library is active from here on);</item>
/// <item>rewrites media fields from paths to placement ids (<see cref="MediaFieldRewriter"/>).</item>
/// </list>
/// Runs as the tenant's system actor. The media folder on disk is left as it was, for rollback;
/// nothing reads it once the library is active.
/// </summary>
public sealed class MediaLibraryImporter(
    IServiceProvider services,
    IMediaLibraryState state,
    IMediaObjectWrites files,
    IMediaObjectStore store,
    IPlacementNameLookup names,
    LibraryFileStore libraryStore,
    MediaLibrarySharingImport sharing,
    IMediaFieldRewriter rewriter,
    IClock clock,
    IOptions<MediaLibraryOptions> options,
    ILogger<MediaLibraryImporter> logger)
{
    public const string DriveName = "Media library";

    /// <summary>Why a version imported without a scan is clean.</summary>
    public const string TrustedReason = "imported, pre-existing";

    private static readonly FileExtensionContentTypeProvider s_contentTypes = new();

    /// <summary>
    /// Imports the tree and activates the library.
    /// </summary>
    /// <param name="trustExisting">
    /// Null takes <see cref="MediaLibraryOptions.TrustExistingFiles"/>. False (the default): every
    /// file goes through <see cref="IUploadQuarantine"/> like an upload and is served once its scan
    /// is clean, so a site's media answers 404 until its scan finishes. True: the operator vouches
    /// for files that were already being served; each is recorded clean with the reason
    /// "imported, pre-existing" and never handed to the scanners: scanned or trusted, never both.
    /// </param>
    public Task<MediaLibraryImportReport> ImportAsync(bool? trustExisting = null, CancellationToken cancellationToken = default) =>
        MediaCallers.AsSystemAsync(services, () => ImportCoreAsync(trustExisting ?? options.Value.TrustExistingFiles, cancellationToken));

    /// <summary>The library drive, created when missing.</summary>
    public async Task<string> EnsureDriveAsync(CancellationToken cancellationToken = default)
    {
        var current = await state.GetAsync();
        if (current.DrivePlacementId is { } existing && await store.GetPlacementAsync(existing) is not null)
        {
            return existing;
        }

        var drive = await files.CreateDriveAsync(DriveName, DriveType.Shared, MediaOwner.Tenant, cancellationToken);
        var document = await state.LoadForUpdateAsync();
        document.DrivePlacementId = drive.Placement.Id;
        await state.SaveAsync(document);
        return drive.Placement.Id;
    }

    private async Task<MediaLibraryImportReport> ImportCoreAsync(bool trustExisting, CancellationToken cancellationToken)
    {
        var report = new MediaLibraryImportReport();
        var driveId = await EnsureDriveAsync(cancellationToken);

        var document = await state.LoadForUpdateAsync();
        document.ImportStartedUtc ??= clock.UtcNow;
        document.TrustedExisting = trustExisting;
        await state.SaveAsync(document);

        var imported = new List<ImportedPlacement> { new(string.Empty, driveId, IsFolder: true) };
        await ImportFolderAsync(libraryStore.Legacy, string.Empty, driveId, trustExisting, report, imported, cancellationToken);
        report.SharingEntries = await sharing.ImportAsync(driveId, imported, cancellationToken);

        document = await state.LoadForUpdateAsync();
        document.ImportedUtc ??= clock.UtcNow;
        document.LastReport = report;
        await state.SaveAsync(document);
        await store.CommitAsync();

        report.ContentItemsRewritten = await rewriter.RewriteAsync(cancellationToken);
        document = await state.LoadForUpdateAsync();
        document.FieldsRewrittenUtc = clock.UtcNow;
        document.LastReport = report;
        await state.SaveAsync(document);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
            "Media library import: {Folders} folders and {Files} files imported, {Skipped} already there, {Failures} failed, {Entries} sharing entries, {Items} content items rewritten.",
            report.FoldersCreated, report.FilesImported, report.Skipped, report.Failures.Count, report.SharingEntries, report.ContentItemsRewritten);
        }

        return report;
    }

    private async Task ImportFolderAsync(IFileStore legacy, string path, string folderId, bool trustExisting, MediaLibraryImportReport report, List<ImportedPlacement> imported, CancellationToken cancellationToken)
    {
        var entries = new List<IFileStoreEntry>();
        await foreach (var entry in legacy.GetDirectoryContentAsync(path))
        {
            entries.Add(entry);
        }

        foreach (var entry in entries.OrderBy(entry => entry.IsDirectory ? 0 : 1).ThenBy(entry => entry.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var childPath = MediaLibraryPaths.Normalize(entry.Path);
            try
            {
                var existing = await names.FindChildAsync([folderId], entry.Name);
                if (entry.IsDirectory)
                {
                    var childId = existing?.Placement.Id;
                    if (childId is null)
                    {
                        childId = (await files.CreateFolderAsync(folderId, entry.Name, cancellationToken)).Placement.Id;
                        report.FoldersCreated++;
                    }

                    imported.Add(new ImportedPlacement(childPath, childId, IsFolder: true));
                    await ImportFolderAsync(legacy, entry.Path, childId, trustExisting, report, imported, cancellationToken);
                    continue;
                }

                if (existing is not null)
                {
                    report.Skipped++;
                    imported.Add(new ImportedPlacement(childPath, existing.Placement.Id, IsFolder: false));
                    continue;
                }

                MediaItem item;
                await using (var bytes = await legacy.GetFileStreamAsync(entry))
                {
                    var claimedType = s_contentTypes.TryGetContentType(entry.Name, out var type) ? type : null;
                    var upload = new MediaUpload(folderId, entry.Name, bytes, claimedType);
                    item = trustExisting
                        ? await files.ImportTrustedAsync(upload, TrustedReason, cancellationToken)
                        : await files.UploadAsync(upload, cancellationToken);
                }

                report.FilesImported++;
                imported.Add(new ImportedPlacement(childPath, item.Placement.Id, IsFolder: false));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not import media '{Path}' into the media library.", childPath);
                report.Failures.Add($"{childPath}: {exception.Message}");
            }
        }
    }
}

/// <summary>A path of the media folder and the placement it became.</summary>
public sealed record ImportedPlacement(string Path, string PlacementId, bool IsFolder);
