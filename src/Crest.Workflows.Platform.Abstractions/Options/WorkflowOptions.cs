namespace Crest.Workflows.Platform.Options;

public class WorkflowOptions
{
    public WorkflowOptions()
    {
        ActivityDictionary = [];
    }

    /// <summary>
    /// The set of activities available to workflows.
    /// Modules can register and unregister activities.
    /// </summary>
    private Dictionary<Type, ActivityRegistration> ActivityDictionary { get; }

    public IEnumerable<Type> ActivityTypes => ActivityDictionary.Values.Select(x => x.ActivityType).ToList().AsReadOnly();

    public WorkflowOptions RegisterActivity(Type activityType)
    {
        ActivityDictionary.TryAdd(activityType, new ActivityRegistration(activityType));

        return this;
    }

    public WorkflowOptions UnregisterActivityType(Type activityType)
    {
        if (!ActivityDictionary.ContainsKey(activityType))
        {
            throw new InvalidOperationException("The specified activity type is not registered.");
        }

        ActivityDictionary.Remove(activityType);
        return this;
    }

    public bool IsActivityRegistered(Type activityType)
    {
        return ActivityDictionary.ContainsKey(activityType);
    }
}

public static class WorkflowOptionsExtensions
{
    public static WorkflowOptions RegisterActivityType<T>(this WorkflowOptions options)
    {
        return options.RegisterActivity(typeof(T));
    }

    public static WorkflowOptions UnregisterActivityType<T>(this WorkflowOptions options)
    {
        return options.UnregisterActivityType(typeof(T));
    }

    public static bool IsActivityRegistered<T>(this WorkflowOptions options)
    {
        return options.IsActivityRegistered(typeof(T));
    }
}
