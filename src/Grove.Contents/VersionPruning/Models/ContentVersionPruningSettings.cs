using System.ComponentModel.DataAnnotations;
using Grove.Contents.Settings;
using Grove.Infrastructure.Settings;
using Grove.Validation;

namespace Grove.Contents.VersionPruning.Models;

public sealed class ContentVersionPruningSettings
{
    public const string GroupId = "ContentVersionPruningSettings";

    /// <summary>
    /// Whether the pruning background task is disabled.
    /// </summary>
    [Display(Name = "Disable", Description = "Whether the pruning task is disabled.", Order = 0)]
    public bool Disabled { get; set; }

    /// <summary>
    /// The number of days after which non-latest, non-published content item versions are deleted.
    /// </summary>
    [Display(Name = "Retention period (days)", Description = "Non-latest, non-published versions older than this number of days will be deleted.", Order = 1)]
    [GreaterThanOrEqual(0)]
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// The number of the most-recent archived (non-latest, non-published) versions to retain
    /// per content item, regardless of age.
    /// </summary>
    [Display(Name = "Archived versions to keep per item", Description = "The number of most-recent archived (non-latest, non-published) versions to always retain per content item, regardless of age. Set to 0 to disable this protection.", Order = 2)]
    [GreaterThanOrEqual(0)]
    public int VersionsToKeep { get; set; } = 1;

    /// <summary>
    /// The content types to prune. When empty, no content types are pruned.
    /// </summary>
    [Display(Name = "Content types to prune", Description = "Select the content types whose old archived versions should be pruned. When no content type is selected, nothing is pruned.", Order = 3)]
    [Setting(Options = typeof(ContentTypeOptions))]
    public string[] ContentTypes { get; set; } = [];
}
