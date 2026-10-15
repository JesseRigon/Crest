using Grove.Workflows.Mediator.Abstractions;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Models;

namespace Grove.Workflows.Mediator.Channels;

/// <inheritdoc cref="Grove.Workflows.Mediator.Contracts.IJobsChannel" />
public class JobsChannel : ChannelBase<EnqueuedJob>, IJobsChannel
{
}