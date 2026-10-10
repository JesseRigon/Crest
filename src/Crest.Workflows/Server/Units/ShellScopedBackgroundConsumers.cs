using System.Threading.Channels;
using Crest.Workflows.Mediator;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Middleware.Command;
using Crest.Workflows.Mediator.Middleware.Notification;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Extensions;
using Crest.Workflows.Management;
using Crest.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;

namespace Crest.Workflows.Units;

/// <summary>
/// The engine's background channels, consumed in shell scopes (docs/workflows.md › Posting
/// on workflows, audit 2026-10-01). The engine registers hosted services for its
/// <c>CommandStrategy.Background</c> commands (DispatchWorkflow, BulkDispatchWorkflows, the
/// background stimulus and event dispatchers) and background notifications, but a tenant
/// container's hosted services are never started by Crest, and the engine's consumers would
/// run each message in a bare service scope where no Crest transaction commits. This reads
/// the same channels and runs each message in a shell scope of its own - one unit, with the
/// after-commit queue, hooks and background jobs all working. Started when the tenant
/// activates, stopped when it terminates.
/// </summary>
public sealed class ShellScopedBackgroundConsumers(
    ICommandsChannel commands,
    INotificationsChannel notifications,
    IShellHost shellHost,
    ShellSettings shellSettings,
    ILogger<ShellScopedBackgroundConsumers> logger)
{
    private CancellationTokenSource? _cts;
    private Task? _commandLoop;
    private Task? _notificationLoop;

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _commandLoop = Task.Run(() => ConsumeAsync(commands.Reader, RunCommandAsync, "command", _cts.Token));
        _notificationLoop = Task.Run(() => ConsumeAsync(notifications.Reader, RunNotificationAsync, "notification", _cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await Task.WhenAll(_commandLoop ?? Task.CompletedTask, _notificationLoop ?? Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
    }

    private async Task ConsumeAsync<T>(ChannelReader<T> reader, Func<T, CancellationToken, Task> run, string kind, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var scope = await shellHost.GetScopeAsync(shellSettings);
                    await scope.UsingAsync(_ => run(message, cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "A background {Kind} failed.", kind);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static Task RunCommandAsync(CommandContext context, CancellationToken cancellationToken) =>
        ShellScope.Services.GetRequiredService<ICommandSender>().SendAsync(context.Command, CommandStrategy.Default, context.Headers, cancellationToken);

    private static Task RunNotificationAsync(NotificationContext context, CancellationToken cancellationToken) =>
        ShellScope.Services.GetRequiredService<INotificationSender>().SendAsync(context.Notification, NotificationStrategy.Sequential, cancellationToken);
}

/// <summary>
/// The engine's bookmark-queue worker, processing in shell scopes: each queued item is
/// resumed in a scope - a unit - of its own, so one long transaction never spans many
/// resumes, and an item whose instance is gone or no longer running is dropped instead of
/// being retried on every tick.
/// </summary>
public sealed class ShellScopedBookmarkQueueWorker(IBookmarkQueueSignaler signaler, IServiceScopeFactory scopeFactory, IShellHost shellHost, ShellSettings shellSettings, ILogger<BookmarkQueueWorker> logger)
    : BookmarkQueueWorker(signaler, scopeFactory, logger)
{
    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var batch = new List<Crest.Workflows.Runtime.Entities.BookmarkQueueItem>();
        var readScope = await shellHost.GetScopeAsync(shellSettings);
        await readScope.UsingAsync(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IBookmarkQueueStore>();
            var page = await store.PageAsync(Crest.Workflows.Common.Models.PageArgs.FromRange(0, 50), new Crest.Workflows.Runtime.OrderDefinitions.BookmarkQueueItemOrder<DateTimeOffset>(x => x.CreatedAt, Crest.Workflows.Common.Entities.OrderDirection.Ascending), cancellationToken);
            batch.AddRange(page.Items);
        });

        foreach (var item in batch)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var scope = await shellHost.GetScopeAsync(shellSettings);
            await scope.UsingAsync(async shellScope =>
            {
                var services = shellScope.ServiceProvider;
                var store = services.GetRequiredService<IBookmarkQueueStore>();
                if (item.WorkflowInstanceId is not null)
                {
                    var instance = await services.GetRequiredService<IWorkflowInstanceStore>().FindAsync(new Crest.Workflows.Management.Filters.WorkflowInstanceFilter { Id = item.WorkflowInstanceId }, cancellationToken);
                    if (instance is null || instance.Status != WorkflowStatus.Running)
                    {
                        await store.DeleteAsync(new Crest.Workflows.Runtime.Filters.BookmarkQueueFilter { Id = item.Id }, cancellationToken);
                        return;
                    }
                }

                var responses = (await services.GetRequiredService<IWorkflowResumer>().ResumeAsync(item.CreateBookmarkFilter(), item.Options, cancellationToken)).ToList();
                if (responses.Count > 0)
                {
                    await store.DeleteAsync(new Crest.Workflows.Runtime.Filters.BookmarkQueueFilter { Id = item.Id }, cancellationToken);
                }
            });
        }
    }
}
