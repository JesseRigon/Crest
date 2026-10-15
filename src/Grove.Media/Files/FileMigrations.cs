#nullable enable
using Grove.Data.Migration;
using YesSql.Sql;

namespace Grove.Media.Files;

public sealed class FileMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<MediaContentIndex>(table => table
            .Column<string>(nameof(MediaContentIndex.ContentId), column => column.WithLength(26))
            .Column<int>(nameof(MediaContentIndex.Kind))
            .Column<int>(nameof(MediaContentIndex.DriveType), column => column.Nullable())
            .Column<int>(nameof(MediaContentIndex.OwnerKind))
            .Column<string>(nameof(MediaContentIndex.OwnerId), column => column.Nullable().WithLength(255))
            .Column<bool>(nameof(MediaContentIndex.IsInactive)));

        await SchemaBuilder.AlterIndexTableAsync<MediaContentIndex>(table =>
        {
            table.CreateIndex("IDX_MediaContentIndex_ContentId", "DocumentId", nameof(MediaContentIndex.ContentId));
            table.CreateIndex("IDX_MediaContentIndex_Kind", "DocumentId", nameof(MediaContentIndex.Kind), nameof(MediaContentIndex.DriveType));
            table.CreateIndex("IDX_MediaContentIndex_Owner", "DocumentId", nameof(MediaContentIndex.OwnerKind), nameof(MediaContentIndex.OwnerId));
        });

        await SchemaBuilder.CreateMapIndexTableAsync<MediaPlacementIndex>(table => table
            .Column<string>(nameof(MediaPlacementIndex.PlacementId), column => column.WithLength(26))
            .Column<string>(nameof(MediaPlacementIndex.ContentId), column => column.WithLength(26))
            .Column<string>(nameof(MediaPlacementIndex.ParentId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(MediaPlacementIndex.DriveId), column => column.WithLength(26))
            .Column<string>(nameof(MediaPlacementIndex.Name), column => column.WithLength(MediaNames.MaxLength))
            .Column<string>(nameof(MediaPlacementIndex.LinkTargetPlacementId), column => column.Nullable().WithLength(26))
            .Column<bool>(nameof(MediaPlacementIndex.IsSoftLink))
            .Column<string>(nameof(MediaPlacementIndex.AccessId), column => column.WithLength(26))
            .Column<int>(nameof(MediaPlacementIndex.Kind)));

        await SchemaBuilder.AlterIndexTableAsync<MediaPlacementIndex>(table =>
        {
            table.CreateIndex("IDX_MediaPlacementIndex_PlacementId", "DocumentId", nameof(MediaPlacementIndex.PlacementId));
            table.CreateIndex("IDX_MediaPlacementIndex_Parent", "DocumentId", nameof(MediaPlacementIndex.ParentId), nameof(MediaPlacementIndex.Name));
            table.CreateIndex("IDX_MediaPlacementIndex_Drive", "DocumentId", nameof(MediaPlacementIndex.DriveId));
            table.CreateIndex("IDX_MediaPlacementIndex_Content", "DocumentId", nameof(MediaPlacementIndex.ContentId), nameof(MediaPlacementIndex.IsSoftLink));
            table.CreateIndex("IDX_MediaPlacementIndex_Target", "DocumentId", nameof(MediaPlacementIndex.LinkTargetPlacementId));
            table.CreateIndex("IDX_MediaPlacementIndex_Access", "DocumentId", nameof(MediaPlacementIndex.AccessId));
        });

        await SchemaBuilder.CreateMapIndexTableAsync<MediaAncestorIndex>(table => table
            .Column<string>(nameof(MediaAncestorIndex.PlacementId), column => column.WithLength(26))
            .Column<string>(nameof(MediaAncestorIndex.AncestorPlacementId), column => column.WithLength(26))
            .Column<string>(nameof(MediaAncestorIndex.AncestorContentId), column => column.WithLength(26))
            .Column<int>(nameof(MediaAncestorIndex.Depth)));

        await SchemaBuilder.AlterIndexTableAsync<MediaAncestorIndex>(table =>
        {
            table.CreateIndex("IDX_MediaAncestorIndex_Placement", "DocumentId", nameof(MediaAncestorIndex.PlacementId), nameof(MediaAncestorIndex.AncestorContentId));
            table.CreateIndex("IDX_MediaAncestorIndex_Ancestor", "DocumentId", nameof(MediaAncestorIndex.AncestorPlacementId));
        });

        await SchemaBuilder.CreateMapIndexTableAsync<PersonalDriveAccessIndex>(table => table
            .Column<string>(nameof(PersonalDriveAccessIndex.AccessId), column => column.WithLength(26))
            .Column<string>(nameof(PersonalDriveAccessIndex.DriveId), column => column.WithLength(26))
            .Column<string>(nameof(PersonalDriveAccessIndex.AdministratorUserId), column => column.WithLength(255))
            .Column<bool>(nameof(PersonalDriveAccessIndex.IsClosed)));

        await SchemaBuilder.AlterIndexTableAsync<PersonalDriveAccessIndex>(table =>
        {
            table.CreateIndex("IDX_PersonalDriveAccessIndex_AccessId", "DocumentId", nameof(PersonalDriveAccessIndex.AccessId));
            table.CreateIndex("IDX_PersonalDriveAccessIndex_Drive", "DocumentId", nameof(PersonalDriveAccessIndex.DriveId), nameof(PersonalDriveAccessIndex.AdministratorUserId), nameof(PersonalDriveAccessIndex.IsClosed));
        });

        return 1;
    }
}
