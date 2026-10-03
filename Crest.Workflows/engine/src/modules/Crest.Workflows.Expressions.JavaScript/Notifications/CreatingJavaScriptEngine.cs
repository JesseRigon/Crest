using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Expressions.JavaScript.Notifications;

/// <summary>
/// This notification is published when a JavaScript engine is being created.
/// </summary>
/// <param name="Options">The options to use when creating the JavaScript engine.</param>
/// <param name="Context">The context in which the JavaScript engine is being created.</param>
public record CreatingJavaScriptEngine(Jint.Options Options, ExpressionExecutionContext Context) : INotification;