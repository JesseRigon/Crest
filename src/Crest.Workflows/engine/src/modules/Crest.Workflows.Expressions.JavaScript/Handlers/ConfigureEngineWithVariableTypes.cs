using Crest.Workflows.Expressions.JavaScript.Notifications;
using Crest.Workflows.Extensions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Expressions.JavaScript.Handlers;

[UsedImplicitly]
public class ConfigureEngineWithWorkflowVariableTypes(IOptions<ManagementOptions> options) 
    : INotificationHandler<EvaluatingJavaScript>
{
    private static readonly Type[] BlacklistedTypes =
    [
        typeof(string),
        typeof(object),
        // Add more types if needed.
    ];

    /// <inheritdoc />
    public Task HandleAsync(EvaluatingJavaScript notification, CancellationToken cancellationToken)
    {
        var engine = notification.Engine;
        var variableTypes = options.Value.VariableDescriptors
            .Where(x => x.Type is { ContainsGenericParameters: false } && !BlacklistedTypes.Contains(x.Type) && !x.Type.IsPrimitive)
            .Select(x => x.Type)
            .ToArray();
        
        foreach (var variableType in variableTypes)
        {
            engine.RegisterType(variableType);
        }                
        
        return Task.CompletedTask;
    }
}
