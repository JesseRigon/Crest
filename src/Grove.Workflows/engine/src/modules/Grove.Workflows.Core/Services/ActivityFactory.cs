using Grove.Workflows.Models;

namespace Grove.Workflows;

/// <inheritdoc />
public class ActivityFactory : IActivityFactory
{
    /// <inheritdoc />
    public IActivity Create(Type type, ActivityConstructorContext context)
    {
        return context.CreateActivity(type).Activity;
    }
}