#nullable enable

using System.Net.Mime;
using System.Text.Json.Nodes;
using System.Text;
using Grove.Api;
using Grove.Contents.Management;
using Grove.Contents.Deployment.AddToDeploymentPlan;
using Grove.Contents.Deployment.ExportContentToDeploymentTarget;
using Grove.Deployment;
using Grove.Deployment.Services;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using ISession = YesSql.ISession;

namespace Grove.Contents.Api;

/// <summary>
/// The content list's deployment actions (the Contents deployment features): add items to a
/// deployment plan, export them to a deployment target through the plan the settings name, and
/// download an item as JSON. Each answers 404 while its feature is disabled; the list page asks
/// <c>content-items/deployment</c> which ones it may offer.
/// </summary>
internal static class ContentDeploymentApi
{
    public const string AddToDeploymentPlanFeatureId = "Grove.Contents.Deployment.AddToDeploymentPlan";
    public const string ExportToDeploymentTargetFeatureId = "Grove.Contents.Deployment.ExportContentToDeploymentTarget";
    public const string DownloadFeatureId = "Grove.Contents.Deployment.Download";

    /// <summary>The deployment target id of a plan's package downloaded by the browser.</summary>
    public const string FileDownloadTargetId = "file";

    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("deployment", ApiHandlers.Handle<ContentDeploymentHandlers>(handlers => handlers.GetActionsAsync()));
        routes.MapPost("deployment/plans/{planId:long}", (HttpContext __http, [FromRoute] long planId, [FromBody] ContentItemSelection request) => ApiHandlers.For<ContentDeploymentHandlers>(__http).AddToPlanAsync(planId, request));
        routes.MapGet("{contentItemId}/json", (HttpContext __http, [FromRoute] string contentItemId, [FromQuery] bool latest = false) => ApiHandlers.For<ContentDeploymentHandlers>(__http).DownloadAsync(contentItemId, latest));
    }
}

public sealed class ContentDeploymentHandlers(
    IAuthorizationService authorizationService,
    IShellFeaturesManager shellFeaturesManager,
    IContentManager contentManager,
    ISession session,
    IServiceProvider services) : ApiHandlers
{
    /// <summary>The deployment actions the caller may run on content items, by feature.</summary>
    public async Task<IResult> GetActionsAsync()
    {
        var enabled = (await shellFeaturesManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
        var canExport = await authorizationService.AuthorizeAsync(User, DeploymentPermissions.Export);
        var canManagePlans = canExport && await authorizationService.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);

        ContentDeploymentPlan[]? plans = null;
        if (enabled.Contains(ContentDeploymentApi.AddToDeploymentPlanFeatureId) && canManagePlans)
        {
            var stored = await session.Query<DeploymentPlan, Grove.Deployment.Indexes.DeploymentPlanIndex>().OrderBy(index => index.Name).ListAsync();
            plans = [.. stored.Select(plan => new ContentDeploymentPlan(plan.Id, plan.Name))];
        }

        long? exportPlanId = null;
        ContentDeploymentTarget[]? targets = null;
        if (enabled.Contains(ContentDeploymentApi.ExportToDeploymentTargetFeatureId) && canExport)
        {
            var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<ExportContentToDeploymentTargetSettings>();
            if (settings.ExportContentToDeploymentTargetPlanId != 0
                && await session.GetAsync<DeploymentPlan>(settings.ExportContentToDeploymentTargetPlanId) is not null)
            {
                exportPlanId = settings.ExportContentToDeploymentTargetPlanId;
                var list = new List<ContentDeploymentTarget> { new(ContentDeploymentApi.FileDownloadTargetId, "File Download", "Download a deployment plan locally.") };
                if (services.GetService<IRemoteDeploymentTargets>() is { } remote
                    && await authorizationService.AuthorizeAsync(User, DeploymentPermissions.ExportRemoteInstances))
                {
                    list.AddRange((await remote.ListAsync())
                        .OrderBy(target => target.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(target => new ContentDeploymentTarget(target.Id, target.Name, "Export to the remote instance.")));
                }

                targets = [.. list];
            }
        }

        var download = enabled.Contains(ContentDeploymentApi.DownloadFeatureId) && canExport;

        return Ok(new ContentDeploymentActions(plans, exportPlanId, targets, download));
    }

    /// <summary>Adds a content item step per item to a deployment plan (the published version is exported).</summary>
    public async Task<IResult> AddToPlanAsync(long planId, [FromBody] ContentItemSelection request)
    {
        if (!await IsEnabledAsync(ContentDeploymentApi.AddToDeploymentPlanFeatureId))
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan)
            || !await authorizationService.AuthorizeAsync(User, DeploymentPermissions.Export))
        {
            return Forbid();
        }

        var plan = await session.GetAsync<DeploymentPlan>(planId);
        if (plan is null)
        {
            return NotFound();
        }

        var factory = services.GetServices<IDeploymentStepFactory>().FirstOrDefault(factory => factory.Name == nameof(ContentItemDeploymentStep));
        if (factory is null)
        {
            return BadRequest();
        }

        var added = 0;
        foreach (var contentItemId in request.ContentItemIds!.Distinct(StringComparer.Ordinal))
        {
            var item = await contentManager.GetAsync(contentItemId);
            if (item is null)
            {
                return NotFound();
            }

            // Export is the overriding permission; EditContent lets a custom permission deny an item.
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item))
            {
                return Forbid();
            }

            var step = (ContentItemDeploymentStep)factory.Create();
            step.ContentItemId = item.ContentItemId;
            plan.DeploymentSteps.Add(step);
            added++;
        }

        await session.SaveAsync(plan);

        return Ok(new ContentDeploymentResult(true, added));
    }

    /// <summary>An item's published (or latest) version as a JSON file.</summary>
    public async Task<IResult> DownloadAsync(string contentItemId, bool latest)
    {
        if (!await IsEnabledAsync(ContentDeploymentApi.DownloadFeatureId))
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, DeploymentPermissions.Export))
        {
            return Forbid();
        }

        var item = await contentManager.GetAsync(contentItemId, latest ? VersionOptions.Latest : VersionOptions.Published);
        if (item is null)
        {
            return NotFound();
        }

        // Export is the overriding permission; EditContent lets a custom permission deny an item.
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item))
        {
            return Forbid();
        }

        return File(Encoding.UTF8.GetBytes(JObject.FromObject(item).ToString()), MediaTypeNames.Application.Json, $"{item.ContentItemId}.json");
    }

    private async Task<bool> IsEnabledAsync(string featureId) =>
        (await shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => string.Equals(feature.Id, featureId, StringComparison.Ordinal));
}

/// <summary>Content items an action applies to.</summary>
public sealed record ContentItemSelection([property: NotEmpty] string[]? ContentItemIds);

/// <summary>
/// The deployment actions offered on the content list: the plans items can be added to, the
/// targets items can be exported to through <paramref name="ExportPlanId"/> (null: not offered),
/// and whether an item can be downloaded as JSON.
/// </summary>
public sealed record ContentDeploymentActions(ContentDeploymentPlan[]? Plans, long? ExportPlanId, ContentDeploymentTarget[]? Targets, bool Download);

public sealed record ContentDeploymentPlan(long Id, string Name);

public sealed record ContentDeploymentTarget(string Id, string Name, string Description);

public sealed record ContentDeploymentResult(bool Succeeded, int Count);

/// <summary>The filters modules add to the content items list (<see cref="Grove.Contents.Services.IContentItemListFilter"/>).</summary>
internal static class ContentItemListFilters
{
    public static async Task<IResult> DescribeAsync(HttpContext httpContext, string? contentType)
    {
        // The choices are tenant data (cultures, taxonomy terms): for the admin's list page.
        if (!await httpContext.RequestServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, Grove.Infrastructure.Shells.Admin.AdminPermissions.AccessAdminPanel))
        {
            return TypedResults.Forbid();
        }

        var descriptors = new List<Grove.Contents.Services.ContentItemListFilterDescriptor>();
        foreach (var filter in httpContext.RequestServices.GetServices<Grove.Contents.Services.IContentItemListFilter>())
        {
            descriptors.AddRange(await filter.DescribeAsync(string.IsNullOrWhiteSpace(contentType) ? null : contentType, httpContext.RequestAborted));
        }

        return TypedResults.Json(descriptors);
    }
}
