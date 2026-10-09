using Crest.Workflows.Scheduling;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Crest.Environment.Shell.Scope;
using Crest.Modules;

namespace Crest.Workflows.Timers.Common;

/// <summary>
/// Creates new schedules when using the default scheduler (which doesn't have its own persistence layer like Quartz or Hangfire).
/// </summary>
[UsedImplicitly]
public class CreateSchedules(IServiceScopeFactory scopeFactory) : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            using var serviceScope = scopeFactory.CreateScope();
            var serviceProvider = serviceScope.ServiceProvider;
            var (triggers, bookmarks) = await GetTriggersAndBookmarksAsync(serviceProvider);
            var triggerScheduler = serviceProvider.GetRequiredService<ITriggerScheduler>();
            var bookmarkScheduler = serviceProvider.GetRequiredService<IBookmarkScheduler>();
            await triggerScheduler.ScheduleAsync(triggers);
            await bookmarkScheduler.ScheduleAsync(bookmarks);
        });
        return Task.CompletedTask;
    }

    public override async Task TerminatingAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var (triggers, bookmarks) = await GetTriggersAndBookmarksAsync(serviceProvider);
        var triggerScheduler = serviceProvider.GetRequiredService<ITriggerScheduler>();
        var bookmarkScheduler = serviceProvider.GetRequiredService<IBookmarkScheduler>();
        await triggerScheduler.UnscheduleAsync(triggers);
        await bookmarkScheduler.UnscheduleAsync(bookmarks);
    }

    private async Task<(List<StoredTrigger>, List<StoredBookmark>)> GetTriggersAndBookmarksAsync(IServiceProvider serviceProvider)
    {
        var stimulusNames = new[]
        {
            SchedulingStimulusNames.Cron, SchedulingStimulusNames.Timer, SchedulingStimulusNames.StartAt, SchedulingStimulusNames.Delay,
        };
        var triggerFilter = new TriggerFilter
        {
            Names = stimulusNames
        };
        var bookmarkFilter = new BookmarkFilter
        {
            Names = stimulusNames
        };
        
        var triggerStore = serviceProvider.GetRequiredService<ITriggerStore>();
        var bookmarkStore = serviceProvider.GetRequiredService<IBookmarkStore>();
        var triggers = (await triggerStore.FindManyAsync(triggerFilter)).ToList();
        var bookmarks = (await bookmarkStore.FindManyAsync(bookmarkFilter)).ToList();
        
        return (triggers, bookmarks);
    }
}