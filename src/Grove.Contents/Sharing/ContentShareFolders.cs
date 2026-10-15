using Grove.Access;
using Grove.ContentManagement;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Sharing;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace Grove.Contents.Sharing;

/// <summary>
/// Where a content item is filed for sharing (docs/sharing.md, requirement 74): a folder of any
/// shareable kind (a client folder in a drive). Filed, the item follows the folder's chain
/// instead of its content type's, and keeps its content type as its type for rules. Stored on the
/// item, so it moves with its versions; empty when the item is not filed.
/// </summary>
public sealed class ContentShareFolderPart : ContentPart
{
    public string? FolderKind { get; set; }

    public string? FolderId { get; set; }

    public ShareableRef? Folder => string.IsNullOrEmpty(FolderKind) || FolderId is null ? null : new ShareableRef(FolderKind, FolderId);
}

/// <summary>One row per filed content item (its latest version): what a folder holds.</summary>
public sealed class ContentShareFolderIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;

    public string FolderKind { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;
}

public sealed class ContentShareFolderIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context) => context
        .For<ContentShareFolderIndex>()
        .When(item => item.Latest && item.Has<ContentShareFolderPart>())
        .Map(item => item.As<ContentShareFolderPart>().Folder is { } folder
            ? new ContentShareFolderIndex { ContentItemId = item.ContentItemId, FolderKind = folder.Kind, FolderId = folder.Id }
            : null);
}

public sealed class ContentShareFolderMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContentShareFolderIndex>(table => table
            .Column<string>(nameof(ContentShareFolderIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(ContentShareFolderIndex.FolderKind), column => column.WithLength(64))
            .Column<string>(nameof(ContentShareFolderIndex.FolderId), column => column.WithLength(255)));

        await SchemaBuilder.AlterIndexTableAsync<ContentShareFolderIndex>(table => table
            .CreateIndex("IDX_ContentShareFolderIndex_Folder", "DocumentId", nameof(ContentShareFolderIndex.FolderKind), nameof(ContentShareFolderIndex.FolderId)));

        await SchemaBuilder.AlterIndexTableAsync<ContentShareFolderIndex>(table => table
            .CreateIndex("IDX_ContentShareFolderIndex_Item", "DocumentId", nameof(ContentShareFolderIndex.ContentItemId)));

        return 1;
    }
}

/// <summary>The content items filed in a folder: they are below it for sharing (recalculation,
/// deleting and moving the folder).</summary>
public sealed class ContentFolderChildSource(ISession session) : IShareableChildSource
{
    public async Task<IReadOnlyList<ShareableRef>> ChildrenAsync(ShareableRef parent, CancellationToken cancellationToken = default)
    {
        if (parent.Kind is ContentShareableKind.KindName or ContentTypeShareableKind.KindName || parent.IsTenant)
        {
            return [];
        }

        var rows = await session.QueryIndex<ContentShareFolderIndex>(index => index.FolderKind == parent.Kind && index.FolderId == parent.Id).ListAsync(cancellationToken);
        return [.. rows.Select(row => new ShareableRef(ContentShareableKind.KindName, row.ContentItemId)).Distinct()];
    }
}

/// <summary>Files content items in folders, and takes them out (requirement 74).</summary>
public interface IContentShareFolders
{
    Task<ShareableRef?> GetFolderAsync(string contentItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Files the item in <paramref name="folder"/> (null: back under its content type). It changes
    /// the item's access, so it is a move (requirement 70): Manager over the item and Editor on
    /// the folder (or on the type, when taking it out).
    /// </summary>
    Task MoveAsync(string contentItemId, ShareableRef? folder, CancellationToken cancellationToken = default);
}

public sealed class ContentShareFolders(
    IContentManager contentManager,
    IShareChangeChecks checks,
    IShareAccessIndex index) : IContentShareFolders
{
    public async Task<ShareableRef?> GetFolderAsync(string contentItemId, CancellationToken cancellationToken = default) =>
        (await contentManager.GetAsync(contentItemId, VersionOptions.Latest))?.As<ContentShareFolderPart>()?.Folder;

    public async Task MoveAsync(string contentItemId, ShareableRef? folder, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest) ?? throw new AccessDeniedException("Sharing.Viewer", "Not found.");
        if (folder is { } target && (target.IsTenant || target.Kind is ContentShareableKind.KindName or ContentTypeShareableKind.KindName))
        {
            throw new ArgumentException("A content item is filed in a folder, not in another item, a type or the tenant.", nameof(folder));
        }

        var self = new ShareableRef(ContentShareableKind.KindName, contentItemId);
        await checks.RequireMoveAsync(self, folder ?? new ShareableRef(ContentTypeShareableKind.KindName, item.ContentType), cancellationToken);

        item.Alter<ContentShareFolderPart>(part =>
        {
            part.FolderKind = folder?.Kind;
            part.FolderId = folder?.Id;
        });
        await contentManager.UpdateAsync(item);

        // Its access follows the new chain at once (the update's handler recalculates too, but
        // only once the item is saved where the kind reads it).
        await index.RecalculateAsync(self, cancellationToken: cancellationToken);
    }
}

/// <summary>Requirement 74's services, while sharing is on.</summary>
[RequireFeatures("Grove.Sharing")]
public sealed class ContentShareFoldersStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProvider<ContentShareFolderIndexProvider>();
        services.AddDataMigration<ContentShareFolderMigrations>();
        services.AddScoped<IShareableChildSource, ContentFolderChildSource>();
        services.AddScoped<IContentShareFolders, ContentShareFolders>();
    }
}
