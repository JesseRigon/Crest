using Crest.BackgroundTasks.Services;
using Crest.Environment.Shell.Scope;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.BackgroundTasks.Controllers;

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

/// <summary>Settings › Background tasks (the native page): every registered task, its schedule and
/// lock settings, enable/disable, and a run now that executes the task once in its own scope.</summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/background-tasks")]
public sealed class BackgroundTasksApiController(
    IAuthorizationService authorization,
    IEnumerable<IBackgroundTask> tasks,
    BackgroundTaskManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackgroundTaskModel>>> ListAsync()
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

    [HttpPut("{name}")]
    public async Task<ActionResult<BackgroundTaskModel>> UpdateAsync(string name, [FromBody] BackgroundTaskUpdate update)
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
    [HttpPost("{name}/run")]
    public async Task<ActionResult<BackgroundTaskRunResult>> RunAsync(string name)
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
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var task = scope.ServiceProvider.GetServices<IBackgroundTask>().GetTaskByName(name);
            try
            {
                await task!.DoWorkAsync(scope.ServiceProvider, HttpContext.RequestAborted);
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
