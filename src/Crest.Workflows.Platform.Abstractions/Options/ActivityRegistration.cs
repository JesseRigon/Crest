namespace Crest.Workflows.Platform.Options;

public class ActivityRegistration
{
    public ActivityRegistration(Type activityType)
    {
        ActivityType = activityType;
    }

    public Type ActivityType { get; }
}
