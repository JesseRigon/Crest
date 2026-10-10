using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Liquid;
using Crest.Liquid;

namespace Crest.Workflows.Platform.Handlers;

using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;

public class LiquidViewTemplateWorkflowExecutionContextHandler : WorkflowExecutionContextHandlerBase
{
    public override async Task EvaluatingExpressionAsync(WorkflowExecutionExpressionContext context)
    {
        if (context.TemplateContext is LiquidTemplateContext liquidTemplateContext)
        {
            var viewContext = liquidTemplateContext.Services.GetRequiredService<ViewContextAccessor>()?.ViewContext;
            await liquidTemplateContext.InitializeAsync(viewContext);
        }
    }
}
