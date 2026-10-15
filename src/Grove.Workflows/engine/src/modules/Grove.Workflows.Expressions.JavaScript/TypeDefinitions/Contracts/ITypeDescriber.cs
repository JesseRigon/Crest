using Grove.Workflows.Expressions.JavaScript.TypeDefinitions.Models;

namespace Grove.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;

/// <summary>
/// Returns a <see cref="TypeDefinition"/> from a given <see cref="Type"/>.
/// </summary>
public interface ITypeDescriber
{
    /// <summary>
    /// Returns a <see cref="TypeDefinition"/> from a given <see cref="Type"/>.
    /// </summary>
    TypeDefinition DescribeType(Type type);
}