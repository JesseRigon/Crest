using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;

namespace Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;

/// <summary>
/// Provides <see cref="VariableDefinition"/>s to the type definition document being constructed.
/// </summary>
public interface IVariableDefinitionProvider
{
    ValueTask<IEnumerable<VariableDefinition>> GetVariableDefinitionsAsync(TypeDefinitionContext context);
}