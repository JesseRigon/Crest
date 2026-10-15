using Grove.DisplayManagement;

namespace Grove.AuditTrail.ViewModels;

public class AuditTrailListViewModel
{
    public IList<IShape> Events { get; set; }
    public AuditTrailIndexOptions Options { get; set; } = new AuditTrailIndexOptions();
    public IShape Pager { get; set; }
    public dynamic Header { get; set; }
}
