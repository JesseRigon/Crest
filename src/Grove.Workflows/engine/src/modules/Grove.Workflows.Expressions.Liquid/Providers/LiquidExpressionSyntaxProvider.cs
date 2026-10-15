using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Extensions;
using Grove.Workflows.Expressions.Liquid.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Expressions.Liquid.Providers;

/// <summary>
/// Provides Liquid expression descriptors.
/// </summary>
public class LiquidExpressionDescriptorProvider : IExpressionDescriptorProvider
{
    /// <summary>
    /// Gets the name of the expression type.
    /// </summary>
    public const string TypeName = "Liquid";

    /// <inheritdoc />
    public IEnumerable<ExpressionDescriptor> GetDescriptors()
    {
        yield return new()
        {
            Type = TypeName,
            DisplayName = "Liquid",
            Properties = new { MonacoLanguage = "liquid" }.ToDictionary(),
            HandlerFactory = ActivatorUtilities.GetServiceOrCreateInstance<LiquidExpressionHandler> 
        };
    }
}