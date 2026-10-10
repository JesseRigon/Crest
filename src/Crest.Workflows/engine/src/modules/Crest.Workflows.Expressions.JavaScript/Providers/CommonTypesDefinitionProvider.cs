using System.Text.Json.Nodes;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Abstractions;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;

namespace Crest.Workflows.Expressions.JavaScript.Providers;

/// <summary>
/// Produces <see cref="TypeDefinition"/>s for common types.
/// </summary>
internal class CommonTypeDefinitionProvider(ITypeDescriber typeDescriber) : TypeDefinitionProvider
{
    protected override IEnumerable<TypeDefinition> GetTypeDefinitions(TypeDefinitionContext context)
    {
        yield return typeDescriber.DescribeType(typeof(Guid));
        yield return typeDescriber.DescribeType(typeof(JsonObject));
        yield return typeDescriber.DescribeType(typeof(Random));
    }
}
