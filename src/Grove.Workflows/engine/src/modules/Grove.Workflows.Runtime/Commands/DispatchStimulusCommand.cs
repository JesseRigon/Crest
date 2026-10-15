using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Models;
using Grove.Workflows.Runtime.Requests;

namespace Grove.Workflows.Runtime.Commands;

public record DispatchStimulusCommand(DispatchStimulusRequest Request) : ICommand<Unit>;