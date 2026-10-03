using JetBrains.Annotations;

namespace Crest.Workflows;

[UsedImplicitly]
public class ActivityStateFilterResult
{
    public string? FilteredValue { get; set; }
    public bool IsFiltered { get; set; }
    
    public static ActivityStateFilterResult Filtered(string filteredValue) => new() { IsFiltered = true, FilteredValue = filteredValue };
    public static ActivityStateFilterResult Pass() => new() { IsFiltered = false };
}