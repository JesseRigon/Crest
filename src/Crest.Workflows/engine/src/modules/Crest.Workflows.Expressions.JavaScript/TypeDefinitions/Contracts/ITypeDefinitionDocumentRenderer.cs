using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;

namespace Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;

/// <summary>
/// Renders a <see cref="TypeDefinitionsDocument"/> to a string.
/// </summary>
public interface ITypeDefinitionDocumentRenderer
{
    /// <summary>
    /// Renders a <see cref="TypeDefinitionsDocument"/> to a string.
    /// </summary>
    string Render(TypeDefinitionsDocument document);
}