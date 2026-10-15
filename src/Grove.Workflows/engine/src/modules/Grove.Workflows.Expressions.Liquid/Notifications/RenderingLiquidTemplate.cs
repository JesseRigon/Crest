using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Mediator.Contracts;
using Fluid;

namespace Grove.Workflows.Expressions.Liquid.Notifications;

public record RenderingLiquidTemplate(TemplateContext TemplateContext, ExpressionExecutionContext Context) : INotification;