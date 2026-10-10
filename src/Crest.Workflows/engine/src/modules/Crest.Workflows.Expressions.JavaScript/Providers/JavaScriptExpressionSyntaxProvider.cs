using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.Expressions.JavaScript.Expressions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Expressions.JavaScript.Providers;

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