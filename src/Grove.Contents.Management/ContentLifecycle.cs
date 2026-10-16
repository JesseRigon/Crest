using Dapper;
using Grove.Contents.Management.Handlers;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace Grove.Contents.Management;

/// <summary>
/// Runs the <see cref="IContentLifecycleHandler"/>s at a lifecycle point
/// (docs/content-engine.md › Behaviour is workflows). Handlers are resolved when first used, not
/// when the content manager is built: a handler running workflows reaches the content manager
/// again through the workflow definition store.
/// </summary>
public sealed class ContentLifecycle(IServiceProvider services, ISession session) : IContentLifecycle
{
    // Items whose hooks are running in this unit: a hook's own write to the item it is about
    // runs no hooks again.
    private readonly HashSet<string> _hooking = new(StringComparer.Ordinal);

    // The previous state a point's hook captured, for the point's trigger after the item is stored.
    private readonly Dictionary<(ContentItem Item, string Point), Previous> _captured = new(new CapturedComparer());

    private IContentLifecycleHandler[] _handlers;

    private IContentLifecycleHandler[] Handlers => _handlers ??= services.GetServices<IContentLifecycleHandler>().ToArray();

    public async Task HookAsync(string point, ContentItem contentItem, ContentPreviousState previous)
    {
        var errors = await CheckAsync(point, contentItem, previous);
        if (errors.Count == 0)
        {
            return;
        }

        // Nothing the refused operation wrote in this unit may commit, and nothing it queued for
        // after the commit (cache evictions) may run.
        await services.GetRequiredService<Grove.Data.Documents.IDocumentStore>().CancelAsync();
        throw new ContentLifecycleRefusedException(point, errors);
    }

    public async Task<IReadOnlyList<ContentLifecycleError>> CheckAsync(string point, ContentItem contentItem, ContentPreviousState previous)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (Handlers.Length == 0)
        {
            return [];
        }

        var state = new Previous(this, previous);
        var key = contentItem.ContentItemId ?? string.Empty;
        if (!_hooking.Add(key))
        {
            state.Close();
            _captured[(contentItem, point)] = state;
            return [];
        }

        try
        {
            var context = new ContentLifecycleContext(point, contentItem, state.ReadAsync);
            foreach (var handler in Handlers)
            {
                await handler.HookAsync(context);
            }

            if (context.Errors.Count > 0)
            {
                return context.Errors;
            }

            // Once the item is stored its stored version is the new state: a trigger that reads the
            // previous one gets it read now.
            foreach (var handler in Handlers)
            {
                if (await handler.NeedsPreviousAfterStoreAsync(point, contentItem.ContentType))
                {
                    await state.ReadAsync();
                    break;
                }
            }

            _captured[(contentItem, point)] = state;
            return [];
        }
        finally
        {
            _hooking.Remove(key);
        }
    }

    public async Task TriggerAsync(string point, ContentItem contentItem, ContentPreviousState previous)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        if (Handlers.Length == 0)
        {
            return;
        }

        if (_captured.Remove((contentItem, point), out var state))
        {
            // Captured before the store when a trigger needed it; otherwise not read at all.
            state.Close();
        }
        else
        {
            state = new Previous(this, previous);
        }

        var context = new ContentLifecycleContext(point, contentItem, state.ReadAsync);
        foreach (var handler in Handlers)
        {
            await handler.TriggerAsync(context);
        }
    }

    // The stored document, read past the session's identity map (whose instance may already carry
    // the change), inside the session's transaction.
    private async Task<ContentItem> ReadStoredAsync(long documentId)
    {
        var configuration = session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName($"{configuration.TablePrefix}{configuration.TableNameConvention.GetDocumentTable()}", configuration.Schema);
        var sql = $"SELECT {dialect.QuoteForColumnName("Content")} FROM {table} WHERE {dialect.QuoteForColumnName("Id")} = @Id";

        var transaction = await session.BeginTransactionAsync();
        var content = await transaction.Connection.QuerySingleOrDefaultAsync<string>(sql, new { Id = documentId }, transaction);
        if (content is null)
        {
            return null;
        }

        var item = (ContentItem)configuration.ContentSerializer.Deserialize(content, typeof(ContentItem));
        item.Id = documentId;
        return item;
    }

    private sealed class Previous(ContentLifecycle owner, ContentPreviousState state)
    {
        private Task<ContentItem> _read;
        private bool _closed;

        public Task<ContentItem> ReadAsync()
        {
            if (_read is not null)
            {
                return _read;
            }

            // After the store, a stored version that was not read before it is the new state.
            if (_closed && state?.CopyOfItem is null)
            {
                return Task.FromResult<ContentItem>(null);
            }

            return _read = state switch
            {
                { CopyOfItem: { } copy } => Task.FromResult(copy),
                { DocumentId: > 0 } => owner.ReadStoredAsync(state.DocumentId),
                _ => Task.FromResult<ContentItem>(null),
            };
        }

        public void Close() => _closed = true;
    }

    private sealed class CapturedComparer : IEqualityComparer<(ContentItem Item, string Point)>
    {
        public bool Equals((ContentItem Item, string Point) x, (ContentItem Item, string Point) y) =>
            ReferenceEquals(x.Item, y.Item) && string.Equals(x.Point, y.Point, StringComparison.Ordinal);

        public int GetHashCode((ContentItem Item, string Point) obj) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item), obj.Point);
    }
}
