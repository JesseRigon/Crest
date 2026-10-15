using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Mediator.Contracts;
using Jint;

namespace Grove.Workflows.Expressions.JavaScript.Notifications;

/// <summary>
/// This notification is published every time a JavaScript expression is about to be evaluated.
/// It gives subscribers a chance to configure the <see cref="Engine"/> with additional functions and variables.
/// </summary>
public record EvaluatingJavaScript(Engine Engine, ExpressionExecutionContext Context, string Expression) : INotification;