using Crest.Workflows.Expressions.Models;

namespace Crest.Workflows.Expressions.Contracts;

/// <summary>
/// Provides descriptors for expression syntaxes.
/// </summary>
public interface IExpressionDescriptorProvider
{
    /// <summary>
    /// Gets the descriptors for the expression syntaxes supported by this provider.
    /// </summary>
    IEnumerable<ExpressionDescriptor> GetDescriptors();
}