using Microsoft.Extensions.Logging;

namespace Grove.Workflows.Common.RecurringTasks;

public interface ISchedule
{
    ScheduledTimer CreateTimer(Func<Task> action, ILogger? logger = null);
}