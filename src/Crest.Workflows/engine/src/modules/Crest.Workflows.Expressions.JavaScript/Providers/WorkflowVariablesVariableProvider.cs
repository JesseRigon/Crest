using Crest.Workflows.Expressions.JavaScript.Options;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Abstractions;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Expressions.JavaScript.Providers;

[UsedImplicitly]
internal class WorkflowVariablesVariableProvider(IOptions<JintOptions> options) : VariableDefinitionProvider
{
    protected override IEnumerable<VariableDefinition> GetVariableDefinitions(TypeDefinitionContext context)
    {
        if(options.Value.DisableWrappers)
            yield break;
        
        yield return CreateVariableDefinition(x => x.Name("variables").Type("WorkflowVariables"));
    }
}