#nullable enable
using Crest.Access;
using Crest.Data.Scoping;
using YesSql;
using YesSql.Services;

namespace Crest.Media.Files;

/// <summary>Where content records and placements live. Scoped; reads and writes go through
/// one session (one transaction), so the loop gate sees the writes it guards.</summary>
public interface IMediaObjectStore
{
    Task<MediaContentDocument?> GetContentAsync(string contentId);

    Task<IReadOnlyList<MediaContentDocument>> GetContentsAsync(IReadOnlyCollection<string> contentIds);

    /// <summary>The personal drive of <paramref name="userId"/>, if one exists.</summary>
    Task<MediaContentDocument?> GetPersonalDriveAsync(string userId);

    Task<MediaPlacementDocument?> GetPlacementAsync(string placementId);

    /// <summary>The hard placements of a content record (soft links are not placements of it).</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetPlacementsOfContentAsync(string contentId);

    /// <summary>The placements directly under any of <paramref name="parentPlacementIds"/>.</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetChildrenAsync(IReadOnlyCollection<string> parentPlacementIds);

    /// <summary>The placements under any of <paramref name="parentPlacementIds"/> named
    /// <paramref name="name"/>, by one indexed query. The database may compare names without
    /// case; callers that need an exact match pick from these.</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetChildrenNamedAsync(IReadOnlyCollection<string> parentPlacementIds, string name);

    /// <summary>Every placement with <paramref name="ancestorPlacementId"/> in its closure.</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetDescendantsAsync(string ancestorPlacementId);

    /// <summary>The soft links that open <paramref name="targetPlacementId"/>.</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetShortcutsToAsync(string targetPlacementId);

    /// <summary>Whether a placement of <paramref name="contentId"/> is in the closure of <paramref name="placementId"/>.</summary>
    Task<bool> HasAncestorContentAsync(string placementId, string contentId);

    /// <summary>Every drive's placement.</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetDrivesAsync();

    /// <summary>The placements directly under any of <paramref name="parentPlacementIds"/> that
    /// <paramref name="scope"/> admits, folders first then by name, one page of them, and how
    /// many it admits in all: filtered and paged in the query (requirement 77).</summary>
    Task<(IReadOnlyList<MediaPlacementDocument> Page, int Total)> GetChildrenAsync(IReadOnlyCollection<string> parentPlacementIds, ScopeRule scope, int skip, int take);

    /// <summary>The drives' placements that <paramref name="scope"/> admits.</summary>
    Task<IReadOnlyList<MediaPlacementDocument>> GetDrivesAsync(ScopeRule scope);

    /// <summary>The drives' placements of one type (with their content's index).</summary>
    Task<IReadOnlyList<MediaContentDocument>> GetDriveContentsAsync(DriveType type);

    /// <summary>The organization drive owned by <paramref name="organizationId"/>'s account.</summary>
    Task<MediaContentDocument?> GetOrganizationDriveAsync(string organizationId);

    Task SaveAsync(MediaContentDocument document);

    Task SaveAsync(MediaPlacementDocument document);

    Task DeleteAsync(MediaContentDocument document);

    Task DeleteAsync(MediaPlacementDocument document);

    /// <summary>Commits what was written so far, so a lock can be released after it.</summary>
    Task CommitAsync();
}

public sealed class YesSqlMediaObjectStore(ISession session) : IMediaObjectStore
{
    public async Task<MediaContentDocument?> GetContentAsync(string contentId) =>
        await session.Query<MediaContentDocument, MediaContentIndex>(index => index.ContentId == contentId).FirstOrDefaultAsync();

    public async Task<IReadOnlyList<MediaContentDocument>> GetContentsAsync(IReadOnlyCollection<string> contentIds)
    {
        if (contentIds.Count == 0)
        {
            return [];
        }

        var ids = contentIds.Distinct(StringComparer.Ordinal).ToArray();
        return [.. await session.Query<MediaContentDocument, MediaContentIndex>(index => index.ContentId.IsIn(ids)).ListAsync()];
    }

    public async Task<MediaContentDocument?> GetPersonalDriveAsync(string userId)
    {
        var personal = (int)DriveType.Personal;
        var user = (int)MediaOwnerKind.User;
        return await session.Query<MediaContentDocument, MediaContentIndex>(index => index.DriveType == personal && index.OwnerKind == user && index.OwnerId == userId).FirstOrDefaultAsync();
    }

    public async Task<MediaPlacementDocument?> GetPlacementAsync(string placementId) =>
        await session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.PlacementId == placementId).FirstOrDefaultAsync();

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetPlacementsOfContentAsync(string contentId) =>
        [.. await session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.ContentId == contentId && !index.IsSoftLink).ListAsync()];

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetChildrenAsync(IReadOnlyCollection<string> parentPlacementIds)
    {
        if (parentPlacementIds.Count == 0)
        {
            return [];
        }

        var ids = parentPlacementIds.Distinct(StringComparer.Ordinal).ToArray();
        return [.. await session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.ParentId.IsIn(ids)).ListAsync()];
    }

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetChildrenNamedAsync(IReadOnlyCollection<string> parentPlacementIds, string name)
    {
        if (parentPlacementIds.Count == 0)
        {
            return [];
        }

        var ids = parentPlacementIds.Distinct(StringComparer.Ordinal).ToArray();
        return [.. await session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.ParentId.IsIn(ids) && index.Name == name).ListAsync()];
    }

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetDescendantsAsync(string ancestorPlacementId) =>
        [.. await session.Query<MediaPlacementDocument, MediaAncestorIndex>(index => index.AncestorPlacementId == ancestorPlacementId).ListAsync()];

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetShortcutsToAsync(string targetPlacementId) =>
        [.. await session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.LinkTargetPlacementId == targetPlacementId).ListAsync()];

    public async Task<bool> HasAncestorContentAsync(string placementId, string contentId) =>
        await session.QueryIndex<MediaAncestorIndex>(index => index.PlacementId == placementId && index.AncestorContentId == contentId).CountAsync() > 0;

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetDrivesAsync() =>
        [.. await session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.ParentId == null && !index.IsSoftLink).ListAsync()];

    public async Task<(IReadOnlyList<MediaPlacementDocument> Page, int Total)> GetChildrenAsync(IReadOnlyCollection<string> parentPlacementIds, ScopeRule scope, int skip, int take)
    {
        if (parentPlacementIds.Count == 0 || scope.Kind == ScopeKind.None)
        {
            return ([], 0);
        }

        var ids = parentPlacementIds.Distinct(StringComparer.Ordinal).ToArray();
        var query = session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.ParentId.IsIn(ids));
        if (scope.Kind == ScopeKind.Filter)
        {
            query = query.Where(ScopeExpressions.ToPredicate<MediaPlacementIndex>(scope));
        }

        var total = await query.CountAsync();
        var page = await query.OrderByDescending(index => index.Kind).ThenBy(index => index.Name).ThenBy(index => index.PlacementId).Skip(skip).Take(take).ListAsync();
        return ([.. page], total);
    }

    public async Task<IReadOnlyList<MediaPlacementDocument>> GetDrivesAsync(ScopeRule scope)
    {
        if (scope.Kind == ScopeKind.None)
        {
            return [];
        }

        var query = session.Query<MediaPlacementDocument, MediaPlacementIndex>(index => index.ParentId == null && !index.IsSoftLink);
        if (scope.Kind == ScopeKind.Filter)
        {
            query = query.Where(ScopeExpressions.ToPredicate<MediaPlacementIndex>(scope));
        }

        return [.. await query.ListAsync()];
    }

    public async Task<IReadOnlyList<MediaContentDocument>> GetDriveContentsAsync(DriveType type)
    {
        var drive = (int)type;
        return [.. await session.Query<MediaContentDocument, MediaContentIndex>(index => index.DriveType == drive).ListAsync()];
    }

    public async Task<MediaContentDocument?> GetOrganizationDriveAsync(string organizationId)
    {
        var organization = (int)DriveType.Organization;
        var owner = (int)MediaOwnerKind.OrganizationAccount;
        return await session.Query<MediaContentDocument, MediaContentIndex>(index => index.DriveType == organization && index.OwnerKind == owner && index.OwnerId == organizationId).FirstOrDefaultAsync();
    }

    public Task SaveAsync(MediaContentDocument document) => session.SaveAsync(document);

    public Task SaveAsync(MediaPlacementDocument document) => session.SaveAsync(document);

    public Task DeleteAsync(MediaContentDocument document)
    {
        session.Delete(document);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(MediaPlacementDocument document)
    {
        session.Delete(document);
        return Task.CompletedTask;
    }

    public Task CommitAsync() => session.SaveChangesAsync();
}
