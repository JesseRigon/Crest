using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Mediator.Contracts;
using Jint;

namespace Grove.Workflows.Expressions.JavaScript.Notifications;

/// <summary>
/// This notification is published every time a JavaScript expression has been evaluated.
/// </summary>
public record EvaluatedJavaScript(Engine Engine, ExpressionExecutionContext Context, string Expression, object? Result) : INotification;