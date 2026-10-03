using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Notifications;

/// <summary>
/// A notification that is sent when an activity is about to execute.
/// </summary>
/// <param name="ActivityExecutionContext">The activity execution context.</param>
public record ActivityExecuting(ActivityExecutionContext ActivityExecutionContext) : INotification;