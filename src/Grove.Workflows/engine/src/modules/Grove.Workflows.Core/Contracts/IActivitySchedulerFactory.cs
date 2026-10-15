namespace Grove.Workflows;

public interface IActivitySchedulerFactory
{
    IActivityScheduler CreateScheduler();
}