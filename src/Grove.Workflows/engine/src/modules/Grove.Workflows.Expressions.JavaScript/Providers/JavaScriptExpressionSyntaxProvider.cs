using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Extensions;
using Grove.Workflows.Expressions.JavaScript.Expressions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Expressions.JavaScript.Providers;

[UsedImplicitly]
internal class JavaScriptExpressionDescriptorProvider : IExpressionDescriptorProvider
{
    private const string TypeName = "JavaScript";

    public IEnumerable<ExpressionDescriptor> GetDescriptors()
    {
        yield return new()
        {
            Type = TypeName,
            DisplayName = "JavaScript",
            Properties = new { MonacoLanguage = "javascript" }.ToDictionary(),
            HandlerFactory = ActivatorUtilities.GetServiceOrCreateInstance<JavaScriptExpressionHandler>
        };
    }
}