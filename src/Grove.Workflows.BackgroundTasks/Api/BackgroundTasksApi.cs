using Grove.Api;
using Grove.BackgroundTasks.Services;
using Grove.Environment.Shell.Scope;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.BackgroundTasks.Api;

/// <param name="DefaultSchedule">The schedule the task ships with (a cron expression).</param>
public sealed record BackgroundTaskModel(
    string Name,
    string? Title,
    string? Description,
    bool Enable,
    string Schedule,
    string DefaultSchedule,
    int LockTimeout,
    int LockExpiration,
    bool UsePipeline);

public sealed record BackgroundTaskUpdate(bool Enable, string? Schedule, string? Description, int LockTimeout, int LockExpiration, bool UsePipeline);

public sealed record BackgroundTaskRunResult(bool Succeeded, string? Error, double Milliseconds);

/// <summary>
/// <c>api/grove/background-tasks</c>, for Settings › Background tasks (the native page): every
/// registered task, its schedule and lock settings, enable/disable, and a run now that executes
/// the task once in its own scope. Every endpoint needs <c>ManageBackgroundTasks</c>.
/// </summary>
public sealed class BackgroundTasksApi : ApiContributor
{
    public override string Name => "BackgroundTasks";

    public override string FeatureId => "Grove.Workflows.BackgroundTasks";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("background-tasks");
        routes.MapGet("", ApiHandlers.Handle<BackgroundTasksHandlers>(h => h.ListAsync()));
        routes.MapPut("{name}", (HttpContext http, [FromRoute] string name, [FromBody] BackgroundTaskUpdate update) =>
            ApiHandlers.For<BackgroundTasksHandlers>(http).UpdateAsync(name, update));
        routes.MapPost("{name}/run", (HttpContext http, [FromRoute] string name) => ApiHandlers.For<BackgroundTasksHandlers>(http).RunAsync(name));
    }
}

public sealed class BackgroundTasksHandlers(
    IAuthorizationService authorization,
    IEnumerable<IBackgroundTask> tasks,
    BackgroundTaskManager manager) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageBackgroundTasks))
        {
            return Forbid();
        }

        var document = await manager.GetDocumentAsync();
        return Ok(tasks
            .Select(task => ToModel(task, document.Settings.TryGetValue(task.GetTaskName(), out var settings) ? settings : null))
            .OrderBy(task => task.Title ?? task.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray());
    }

    public async Task<IResult> UpdateAsync(string name, BackgroundTaskUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageBackgroundTasks))
        {
            return Forbid();
        }

        var task = tasks.GetTaskByName(name);
        if (task is null)
        {
            return NotFound();
        }

        if (update.LockTimeout < 0 || update.LockExpiration < 0)
        {
            return BadRequest("Lock timeout and expiration must be zero or more milliseconds.");
        }

        var defaults = task.GetDefaultSettings();
        var document = await manager.LoadDocumentAsync();
        if (!document.Settings.TryGetValue(name, out var settings))
        {
            settings = defaults;
        }

        settings.Title = defaults.Title;
        settings.Enable = update.Enable;
        settings.Schedule = string.IsNullOrWhiteSpace(update.Schedule) ? defaults.Schedule : update.Schedule.Trim();
        settings.Description = update.Description ?? string.Empty;
        settings.LockTimeout = update.LockTimeout;
        settings.LockExpiration = update.LockExpiration;
        settings.UsePipeline = update.UsePipeline;
        await manager.UpdateAsync(name, settings);

        return Ok(ToModel(task, settings));
    }

    /// <summary>Runs the task once now, in a scope of its own, whatever its schedule or state.</summary>
    public async Task<IResult> RunAsync(string name)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageBackgroundTasks))
        {
            return Forbid();
        }

        if (tasks.GetTaskByName(name) is null)
        {
            return NotFound();
        }

        var started = DateTime.UtcNow;
        string? error = null;
        var aborted = HttpContext.RequestAborted;
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var task = scope.ServiceProvider.GetServices<IBackgroundTask>().GetTaskByName(name);
            try
            {
                await task!.DoWorkAsync(scope.ServiceProvider, aborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = exception.Message;
            }
        });

        return Ok(new BackgroundTaskRunResult(error is null, error, (DateTime.UtcNow - started).TotalMilliseconds));
    }

    private static BackgroundTaskModel ToModel(IBackgroundTask task, BackgroundTaskSettings? stored)
    {
        var defaults = task.GetDefaultSettings();
        var settings = stored ?? defaults;
        return new BackgroundTaskModel(defaults.Name, defaults.Title, settings.Description, settings.Enable, settings.Schedule, defaults.Schedule, settings.LockTimeout, settings.LockExpiration, settings.UsePipeline);
    }
}
