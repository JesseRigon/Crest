namespace Crest.Workflows;

public class ActivitySchedulerFactory : IActivitySchedulerFactory
{
    //public IActivityScheduler CreateScheduler() => new StackBasedActivityScheduler();
    public IActivityScheduler CreateScheduler() => new QueueBasedActivityScheduler();
}