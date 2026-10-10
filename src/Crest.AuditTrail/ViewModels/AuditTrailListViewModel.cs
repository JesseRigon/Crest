using Crest.DisplayManagement;

namespace Crest.AuditTrail.ViewModels;

public class AuditTrailListViewModel
{
    public IList<IShape> Events { get; set; }
    public AuditTrailIndexOptions Options { get; set; } = new AuditTrailIndexOptions();
    public IShape Pager { get; set; }
    public dynamic Header { get; set; }
}
