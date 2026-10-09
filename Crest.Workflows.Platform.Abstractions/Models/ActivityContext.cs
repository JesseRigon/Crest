using Crest.Workflows.Platform.Activities;

namespace Crest.Workflows.Platform.Models;

public class ActivityContext
{
    public ActivityRecord ActivityRecord { get; set; }
    public IActivity Activity { get; set; }
}
