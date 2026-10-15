namespace Grove.Workflows;

public interface IActivityStateFilter
{
    Task<ActivityStateFilterResult> ExecuteAsync(ActivityStateFilterContext context);
}