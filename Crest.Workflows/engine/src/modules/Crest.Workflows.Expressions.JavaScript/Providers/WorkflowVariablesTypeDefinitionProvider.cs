using Crest.Workflows.Extensions;
using Crest.Workflows.Expressions.JavaScript.Extensions;
using Crest.Workflows.Expressions.JavaScript.Options;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Abstractions;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Expressions.JavaScript.Providers;

[UsedImplicitly]
internal class WorkflowVariablesTypeDefinitionProvider(IOptions<JintOptions> options) : TypeDefinitionProvider
{
    protected override IEnumerable<TypeDefinition> GetTypeDefinitions(TypeDefinitionContext context)
    {
        if(options.Value.DisableWrappers)
            yield break;
        
        var variables = context.WorkflowGraph.Workflow.Variables;
        
        var workflowTypeDefinition = new TypeDefinition
        {
            Name = "WorkflowVariables",
            DeclarationKeyword = "class"
        };

        foreach (var variable in variables.Where(x => x.Name.IsValidVariableName()))
        {
            var variableType = variable.GetVariableType();
            workflowTypeDefinition.Properties.Add(new PropertyDefinition
            {
                Name = variable.Name,
                Type = variableType.Name
            });
        }
        
        yield return workflowTypeDefinition;
    }
}