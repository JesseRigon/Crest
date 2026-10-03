using Crest.Workflows.Mediator.Contexts;

namespace Crest.Workflows.Mediator.Contracts;

/// <summary>
/// Represents a strategy for publishing events.
/// </summary>
public interface IEventPublishingStrategy
{
    /// <summary>
    /// Publishes an event.
    /// </summary>
    /// <param name="context">The context for publishing the event.</param>
    Task PublishAsync(NotificationStrategyContext context);
}