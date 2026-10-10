using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Runtime.Commands;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Handlers;

[UsedImplicitly]
public class DispatchStimulusCommandHandler(IStimulusSender stimulusSender) : ICommandHandler<DispatchStimulusCommand>
{
    public async Task<Unit> HandleAsync(DispatchStimulusCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (request.ActivityTypeName != null)
        {
            await stimulusSender.SendAsync(request.ActivityTypeName, request.Stimulus!, request.Metadata, cancellationToken);
            return Unit.Instance;
        }

        if (request.StimulusHash != null)
        {
            await stimulusSender.SendAsync(request.StimulusHash!, request.Metadata, cancellationToken);
            return Unit.Instance;
        }

        throw new InvalidOperationException("Either ActivityTypeName or StimulusHash must be specified.");
    }
}