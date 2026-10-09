using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.Liquid;
using Crest.Modules;

namespace Crest.Workflows.Platform;

using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Evaluators;
using Crest.Workflows.Platform.Expressions;
using Crest.Workflows.Platform.Handlers;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Options;
using Crest.Workflows.Platform.Services;

/// <summary>
/// The platform activity library: the activities platform modules contribute through
/// <c>AddActivity</c> (Email, Users, Contents, Notifications, ...), and the expression,
/// script and context services they evaluate with. <see cref="StockActivityRunner"/> runs
/// them inside the engine. This is what remained of the stock workflows module when it was
/// merged into Crest.Workflows; its engine, stores, designer and control-flow activities
/// were dropped, because the engine has its own.
/// </summary>
[Feature("Crest.Workflows")]
public sealed class PlatformActivitiesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<WorkflowExecutionContext>();
            o.MemberAccessStrategy.Register<WorkflowExecutionContext, LiquidPropertyAccessor>("Input", (obj, context) => new LiquidPropertyAccessor((LiquidTemplateContext)context, (name, context) => LiquidWorkflowExpressionEvaluator.ToFluidValue(obj.Input, name, context)));
            o.MemberAccessStrategy.Register<WorkflowExecutionContext, LiquidPropertyAccessor>("Output", (obj, context) => new LiquidPropertyAccessor((LiquidTemplateContext)context, (name, context) => LiquidWorkflowExpressionEvaluator.ToFluidValue(obj.Output, name, context)));
            o.MemberAccessStrategy.Register<WorkflowExecutionContext, LiquidPropertyAccessor>("Properties", (obj, context) => new LiquidPropertyAccessor((LiquidTemplateContext)context, (name, context) => LiquidWorkflowExpressionEvaluator.ToFluidValue(obj.Properties, name, context)));
        });

        services.AddSingleton<IWorkflowTypeIdGenerator, WorkflowTypeIdGenerator>();
        services.AddSingleton<IWorkflowIdGenerator, WorkflowIdGenerator>();
        services.AddSingleton<IActivityIdGenerator, ActivityIdGenerator>();
        services.AddScoped(typeof(Resolver<>));
        services.AddSingleton<ISecurityTokenService, SecurityTokenService>();
        services.AddScoped<IActivityLibrary, ActivityLibrary>();
        services.AddScoped<IWorkflowExecutionContextHandler, DefaultWorkflowExecutionContextHandler>();
        services.AddScoped<IWorkflowExecutionContextHandler, LiquidViewTemplateWorkflowExecutionContextHandler>();
        services.AddScoped<IWorkflowExpressionEvaluator, LiquidWorkflowExpressionEvaluator>();
        services.AddScoped<IWorkflowScriptEvaluator, JavaScriptWorkflowScriptEvaluator>();
        services.AddHttpClient();

        services.Configure<WorkflowOptions>(options => options
            .RegisterActivityType<NotifyTask>()
            .RegisterActivityType<LogTask>()
            .RegisterActivityType<HttpRequestTask>());
    }
}
