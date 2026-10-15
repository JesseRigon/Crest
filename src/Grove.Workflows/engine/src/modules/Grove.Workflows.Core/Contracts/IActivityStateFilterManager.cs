namespace Grove.Workflows;

public interface IActivityStateFilterManager
{
    Task<string> RunFiltersAsync(ActivityStateFilterContext context);
}