using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Mediator.Contracts;
using Fluid;

namespace Crest.Workflows.Expressions.Liquid.Notifications;

public record RenderingLiquidTemplate(TemplateContext TemplateContext, ExpressionExecutionContext Context) : INotification;