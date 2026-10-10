using Crest.DisplayManagement;

namespace Crest.Deployment.ViewModels;

public class DisplayDeploymentPlanThumbnailViewModel
{
    public string Category { get; set; }

    public string CategoryId { get; set; }

    public IShape Thumbnail { get; set; }

    public string Type { get; set; }
}
