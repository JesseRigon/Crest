using Crest.Workflows.Extensions;
using Crest.Workflows.Expressions.JavaScript.Notifications;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.LogPersistence;
using JetBrains.Annotations;

namespace Crest.Workflows.Expressions.JavaScript.Handlers;

/// <summary>
/// A handler that configures the Jint engine with common types.
/// </summary>
[UsedImplicitly]
public class ConfigureEngineWithCommonTypes : INotificationHandler<EvaluatingJavaScript>
{
    /// <inheritdoc />
    public Task HandleAsync(EvaluatingJavaScript notification, CancellationToken cancellationToken)
    {
        var engine = notification.Engine;
        
        // Add common .NET types.
        engine.RegisterType<DateTime>();
        engine.RegisterType<DateTimeOffset>();
        engine.RegisterType<TimeSpan>();
        engine.RegisterType<Guid>();
        engine.RegisterType<Random>();
        engine.RegisterType<LogPersistenceMode>();
        
        return Task.CompletedTask;
    }
}