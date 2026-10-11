using Crest.Indexing.Models;
using Crest.Infrastructure.Entities;

namespace Crest.Indexing.Handlers;

public abstract class IndexProfileHandlerBase : ModelHandlerBase<IndexProfile>, IIndexProfileHandler
{
    public virtual Task ExportingAsync(IndexProfileExportingContext context)
        => Task.CompletedTask;

    public virtual Task ResetAsync(IndexProfileResetContext context)
        => Task.CompletedTask;

    public virtual Task SynchronizedAsync(IndexProfileSynchronizedContext context)
        => Task.CompletedTask;
}
