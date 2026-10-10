using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Runtime.Requests;

namespace Crest.Workflows.Runtime.Commands;

public record DispatchStimulusCommand(DispatchStimulusRequest Request) : ICommand<Unit>;