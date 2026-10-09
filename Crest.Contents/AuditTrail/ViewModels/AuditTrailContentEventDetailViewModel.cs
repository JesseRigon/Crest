using Crest.ContentManagement;

namespace Crest.Contents.AuditTrail.ViewModels;

public class AuditTrailContentEventDetailViewModel : AuditTrailContentEventViewModel
{
    public string Previous { get; set; }
    public string Current { get; set; }
    public ContentItem PreviousContentItem { get; set; }
}
