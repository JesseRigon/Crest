using Crest.Workflows.Mediator.Abstractions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;

namespace Crest.Workflows.Mediator.Channels;

/// <inheritdoc cref="Crest.Workflows.Mediator.Contracts.IJobsChannel" />
public class JobsChannel : ChannelBase<EnqueuedJob>, IJobsChannel
{
}