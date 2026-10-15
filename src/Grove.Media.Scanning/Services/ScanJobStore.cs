using Grove.Media.Scanning.Indexes;
using Grove.Media.Scanning.Models;
using YesSql;

namespace Grove.Media.Scanning.Services;

/// <summary>Where scan jobs live: the tenant's own store.</summary>
public interface IScanJobStore
{
    Task<ScanJob?> GetAsync(string scanId, CancellationToken cancellationToken = default);

    /// <summary>Saves a job: with the caller's unit of work, or committed at once (<paramref name="commit"/>)
    /// so a verdict is durable before the job's lock is released.</summary>
    Task SaveAsync(ScanJob job, bool commit, CancellationToken cancellationToken = default);

    /// <summary>The jobs whose next attempt is due.</summary>
    Task<IReadOnlyList<string>> ListDueAsync(DateTime now, int max, CancellationToken cancellationToken = default);
}

public sealed class YesSqlScanJobStore(ISession session) : IScanJobStore
{
    public async Task<ScanJob?> GetAsync(string scanId, CancellationToken cancellationToken = default)
        => await session.Query<ScanJob, ScanJobIndex>(index => index.ScanId == scanId).FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(ScanJob job, bool commit, CancellationToken cancellationToken = default)
    {
        await session.SaveAsync(job, cancellationToken: cancellationToken);

        if (commit)
        {
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> ListDueAsync(DateTime now, int max, CancellationToken cancellationToken = default)
    {
        var rows = await session.QueryIndex<ScanJobIndex>(index => index.NextAttemptUtc != null && index.NextAttemptUtc <= now)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(max)
            .ListAsync(cancellationToken);

        return [.. rows.Select(row => row.ScanId)];
    }
}
