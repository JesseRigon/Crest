using Grove.Workflows.Expressions.JavaScript.Options;
using Grove.Workflows.Expressions.JavaScript.TypeDefinitions.Abstractions;
using Grove.Workflows.Expressions.JavaScript.TypeDefinitions.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Expressions.JavaScript.Providers;

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