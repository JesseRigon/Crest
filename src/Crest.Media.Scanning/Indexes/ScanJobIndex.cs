using Crest.Media.Scanning.Models;
using YesSql.Indexes;

namespace Crest.Media.Scanning.Indexes;

public sealed class ScanJobIndex : MapIndex
{
    public string ScanId { get; set; } = string.Empty;

    public string ContentId { get; set; } = string.Empty;

    public int Version { get; set; }

    public int Status { get; set; }

    public DateTime? NextAttemptUtc { get; set; }
}

public sealed class ScanJobIndexProvider : IndexProvider<ScanJob>
{
    public override void Describe(DescribeContext<ScanJob> context)
    {
        context.For<ScanJobIndex>()
            .Map(job => new ScanJobIndex
            {
                ScanId = job.ScanId,
                ContentId = job.ContentId,
                Version = job.Version,
                Status = (int)job.Status,
                NextAttemptUtc = job.NextAttemptUtc,
            });
    }
}
