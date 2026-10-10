namespace Crest.Workflows;

public interface IActivitySchedulerFactory
{
    IActivityScheduler CreateScheduler();
}