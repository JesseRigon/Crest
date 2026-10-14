#nullable enable

using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Deployment.Indexes;
using Crest.Deployment.Services;
using Crest.FileStorage;
using Crest.Json;
using Crest.ContentManagement.Utilities;
using Crest.Recipes.Models;
using Crest.Security.Permissions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Services;

namespace Crest.Deployment.Api;

public sealed class DeploymentApi : ApiContributor
{
    public override string Name => "Deployment";

    public override string FeatureId => "Crest.Deployment";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("deployment");
        routes.MapGet("plans", (HttpContext __http, [FromQuery] string? search = null) => ApiHandlers.For<DeploymentHandlers>(__http).ListPlansAsync(search));
        routes.MapPost("plans", (HttpContext __http, [FromBody] DeploymentPlanName request) => ApiHandlers.For<DeploymentHandlers>(__http).CreatePlanAsync(request));
        routes.MapPut("plans/{id:long}", (HttpContext __http, [FromRoute] long id, [FromBody] DeploymentPlanName request) => ApiHandlers.For<DeploymentHandlers>(__http).RenamePlanAsync(id, request));
        routes.MapPost("plans/delete", (HttpContext __http, [FromBody] DeploymentPlanIds request) => ApiHandlers.For<DeploymentHandlers>(__http).DeletePlansAsync(request));
        routes.MapGet("plans/{id:long}", (HttpContext __http, [FromRoute] long id) => ApiHandlers.For<DeploymentHandlers>(__http).GetPlanAsync(id));
        routes.MapPost("plans/{id:long}/steps", (HttpContext __http, [FromRoute] long id, [FromBody] DeploymentStepWrite request) => ApiHandlers.For<DeploymentHandlers>(__http).AddStepAsync(id, request));
        routes.MapPut("plans/{id:long}/steps/{stepId}", (HttpContext __http, [FromRoute] long id, [FromRoute] string stepId, [FromBody] DeploymentStepWrite request) => ApiHandlers.For<DeploymentHandlers>(__http).SaveStepAsync(id, stepId, request));
        routes.MapDelete("plans/{id:long}/steps/{stepId}", (HttpContext __http, [FromRoute] long id, [FromRoute] string stepId) => ApiHandlers.For<DeploymentHandlers>(__http).DeleteStepAsync(id, stepId));
        routes.MapPost("plans/{id:long}/steps/move", (HttpContext __http, [FromRoute] long id, [FromBody] DeploymentStepMove request) => ApiHandlers.For<DeploymentHandlers>(__http).MoveStepAsync(id, request));
        routes.MapGet("plans/{id:long}/package", (HttpContext __http, [FromRoute] long id) => ApiHandlers.For<DeploymentHandlers>(__http).ExportAsync(id));
        routes.MapPost("import/json", (HttpContext __http, [FromBody] DeploymentJsonImport request) => ApiHandlers.For<DeploymentHandlers>(__http).ImportJsonAsync(request));
        routes.MapPost("import/package", (HttpContext __http, [FromForm] IFormFile? package) => ApiHandlers.For<DeploymentHandlers>(__http).ImportPackageAsync(package)).WithMetadata(new RequestSizeLimitAttribute(256L * 1024 * 1024)).DisableAntiforgery();
    }
}


/// <summary>
/// The Deployments pages' API: plans and their steps, exporting a plan as a package or to a
/// remote instance (Crest.Deployment.Remote's API), importing a package or a recipe.
/// The deployment features own the data; when one is disabled its endpoints answer 404.
/// A step's settings travel as the JSON the plan stores them as, so a step type a module adds
/// is editable without a component of its own.
/// </summary>
public sealed class DeploymentHandlers(IRequestAccess requestAccess, ILogger<DeploymentHandlers> logger) : ApiHandlers
{
    private static readonly string[] s_stepBaseProperties = [nameof(DeploymentStep.Id), nameof(DeploymentStep.Name), nameof(DeploymentStep.Category)];

    // Plans

    public async Task<IResult> ListPlansAsync([FromQuery] string? search = null)
    {
        var access = await AuthorizeAllAsync(DeploymentPermissions.ManageDeploymentPlan, DeploymentPermissions.Export);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var query = access.GetRequiredService<ISession>().Query<DeploymentPlan, DeploymentPlanIndex>();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(index => index.Name.Contains(search));

        var plans = await query.OrderBy(index => index.Name).ListAsync();
        return Ok(plans.Select(plan => new DeploymentPlanSummary(plan.Id, plan.Name, plan.DeploymentSteps.Count)).ToArray());
    }

    public async Task<IResult> CreatePlanAsync([FromBody] DeploymentPlanName request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Failure("The name is mandatory.");

        var session = access.GetRequiredService<ISession>();
        if (await session.QueryIndex<DeploymentPlanIndex>(index => index.Name == name).CountAsync() > 0) return Failure("A deployment plan with the same name already exists.");

        var plan = new DeploymentPlan { Name = name };
        await session.SaveAsync(plan);
        return Ok(new DeploymentResult(true, plan.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public async Task<IResult> RenamePlanAsync(long id, [FromBody] DeploymentPlanName request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var session = access.GetRequiredService<ISession>();
        var plan = await session.GetAsync<DeploymentPlan>(id);
        if (plan is null) return NotFound();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return Failure("The name is mandatory.");
        if (!string.Equals(name, plan.Name, StringComparison.OrdinalIgnoreCase)
            && await session.QueryIndex<DeploymentPlanIndex>(index => index.Name == name && index.DocumentId != id).CountAsync() > 0)
        {
            return Failure("A deployment plan with the same name already exists.");
        }

        plan.Name = name;
        await session.SaveAsync(plan);
        return Ok(new DeploymentResult(true, null));
    }

    public async Task<IResult> DeletePlansAsync([FromBody] DeploymentPlanIds request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var ids = request.Ids?.Distinct().ToArray() ?? [];
        if (ids.Length == 0) return Failure("Select at least one deployment plan.");

        var session = access.GetRequiredService<ISession>();
        foreach (var plan in await session.Query<DeploymentPlan, DeploymentPlanIndex>(index => index.DocumentId.IsIn(ids)).ListAsync())
        {
            session.Delete(plan);
        }

        return Ok(new DeploymentResult(true, null));
    }

    public async Task<IResult> GetPlanAsync(long id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var plan = await access.GetRequiredService<ISession>().GetAsync<DeploymentPlan>(id);
        if (plan is null) return NotFound();

        var options = SerializerOptions(access);
        var steps = plan.DeploymentSteps.Select(step => new DeploymentStepModel(step.Id, step.Name ?? step.GetType().Name, Category(step), Properties(step, options))).ToArray();
        var stepTypes = access.GetRequiredService<IEnumerable<IDeploymentStepFactory>>()
            .Select(factory => (factory.Name, Step: factory.Create()))
            .Select(entry => new DeploymentStepType(entry.Name, Category(entry.Step), Properties(entry.Step, options)))
            .OrderBy(type => type.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(type => type.Type, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var remoteInstances = Array.Empty<DeploymentTargetRef>();
        if (access.GetService<IRemoteDeploymentTargets>() is { } targets && await Authorized(DeploymentPermissions.ExportRemoteInstances))
        {
            remoteInstances = (await targets.ListAsync())
                .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Ok(new DeploymentPlanDetail(plan.Id, plan.Name, steps, stepTypes, remoteInstances, await Authorized(DeploymentPermissions.Export)));
    }

    // Steps

    public async Task<IResult> AddStepAsync(long id, [FromBody] DeploymentStepWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var session = access.GetRequiredService<ISession>();
        var plan = await session.GetAsync<DeploymentPlan>(id);
        if (plan is null) return NotFound();

        if (!TryBuildStep(access, request, Guid.NewGuid().ToString("n"), out var step, out var error)) return Failure(error);

        plan.DeploymentSteps.Add(step);
        await session.SaveAsync(plan);
        return Ok(new DeploymentResult(true, step.Id));
    }

    public async Task<IResult> SaveStepAsync(long id, string stepId, [FromBody] DeploymentStepWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var session = access.GetRequiredService<ISession>();
        var plan = await session.GetAsync<DeploymentPlan>(id);
        var index = plan?.DeploymentSteps.FindIndex(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase)) ?? -1;
        if (plan is null || index < 0) return NotFound();

        if (!TryBuildStep(access, request with { Type = plan.DeploymentSteps[index].Name ?? request.Type }, plan.DeploymentSteps[index].Id, out var step, out var error)) return Failure(error);

        plan.DeploymentSteps[index] = step;
        await session.SaveAsync(plan);
        return Ok(new DeploymentResult(true, step.Id));
    }

    public async Task<IResult> DeleteStepAsync(long id, string stepId)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var session = access.GetRequiredService<ISession>();
        var plan = await session.GetAsync<DeploymentPlan>(id);
        var step = plan?.DeploymentSteps.FirstOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (plan is null || step is null) return NotFound();

        plan.DeploymentSteps.Remove(step);
        await session.SaveAsync(plan);
        return Ok(new DeploymentResult(true, null));
    }

    public async Task<IResult> MoveStepAsync(long id, [FromBody] DeploymentStepMove request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var session = access.GetRequiredService<ISession>();
        var plan = await session.GetAsync<DeploymentPlan>(id);
        if (plan is null) return NotFound();

        var count = plan.DeploymentSteps.Count;
        if (request.OldIndex < 0 || request.OldIndex >= count || request.NewIndex < 0 || request.NewIndex >= count) return Failure("The step position is out of range.");

        var step = plan.DeploymentSteps[request.OldIndex];
        plan.DeploymentSteps.RemoveAt(request.OldIndex);
        plan.DeploymentSteps.Insert(request.NewIndex, step);
        await session.SaveAsync(plan);
        return Ok(new DeploymentResult(true, null));
    }

    // Export

    public async Task<IResult> ExportAsync(long id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.Export);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var plan = await access.GetRequiredService<ISession>().GetAsync<DeploymentPlan>(id);
        if (plan is null) return NotFound();

        var archive = await DeploymentPackages.CreateArchiveAsync(access.GetRequiredService<IDeploymentManager>(), access.GetRequiredService<ITempDirectoryProvider>(), plan);
        try
        {
            var content = await System.IO.File.ReadAllBytesAsync(archive, HttpContext.RequestAborted);
            return File(content, MediaTypeNames.Application.Zip, plan.Name.ToSafeName() + ".zip");
        }
        finally
        {
            System.IO.File.Delete(archive);
        }
    }


    // Import

    public async Task<IResult> ImportJsonAsync([FromBody] DeploymentJsonImport request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.Import);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Json) || !IsJson(request.Json, JOptions.Document)) return Failure("The recipe is written in an incorrect JSON format.");

        return await ImportAsync(() => DeploymentPackages.ImportJsonAsync(access.GetRequiredService<IDeploymentManager>(), access.GetRequiredService<ITempDirectoryProvider>(), request.Json), "Recipe imported successfully.");
    }

    [RequestSizeLimit(256L * 1024 * 1024)]
    public async Task<IResult> ImportPackageAsync(IFormFile? package)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.Import);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();
        if (package is null || package.Length == 0) return Failure("Please add a file to import.");

        // Uploads pass the tenant's upload pipeline (scanning, validation) like any other file.
        await using var uploaded = package.OpenReadStream();
        await using var created = await access.GetRequiredService<FileCreationService>().CreateAsync(
            new FileCreatingContext(package.FileName, package.Length, package.ContentType),
            uploaded,
            HttpContext.RequestAborted);

        if (!created.Succeeded) return Failure(created.ErrorMessage ?? $"The uploaded file '{package.FileName}' was rejected.");

        return await ImportAsync(() => DeploymentPackages.ImportPackageAsync(access.GetRequiredService<IDeploymentManager>(), access.GetRequiredService<ITempDirectoryProvider>(), package.FileName, created.Stream, HttpContext.RequestAborted), "Deployment package imported.");
    }













    private async Task<IResult> ImportAsync(Func<Task> import, string success)
    {
        try
        {
            await import();
            return Ok(new DeploymentResult(true, success));
        }
        catch (NotSupportedException exception)
        {
            return Failure(exception.Message);
        }
        catch (RecipeExecutionException exception)
        {
            logger.LogError(exception, "Unable to import a deployment package.");
            return Failure($"The import failed with the following errors: {string.Join(' ', exception.StepResult.Errors)}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to import a deployment package.");
            return Failure("Unexpected error occurred while importing the deployment package.");
        }
    }

    private bool TryBuildStep(AuthorizedRequest access, DeploymentStepWrite request, string stepId, out DeploymentStep step, out string error)
    {
        step = null!;
        error = string.Empty;

        var factory = access.GetRequiredService<IEnumerable<IDeploymentStepFactory>>().FirstOrDefault(candidate => string.Equals(candidate.Name, request.Type, StringComparison.Ordinal));
        if (factory is null)
        {
            error = $"The step type '{request.Type}' is not available; enable the feature that provides it.";
            return false;
        }

        var template = factory.Create();
        var properties = request.Properties?.DeepClone() as JsonObject ?? [];
        foreach (var key in properties.Select(pair => pair.Key).Where(key => s_stepBaseProperties.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray())
        {
            properties.Remove(key);
        }

        try
        {
            step = (DeploymentStep)(properties.Deserialize(template.GetType(), SerializerOptions(access)) ?? template);
        }
        catch (JsonException exception)
        {
            error = $"The step settings are not valid for {factory.Name}: {exception.Message}";
            return false;
        }

        step.Id = stepId;
        step.Name = template.Name ?? factory.Name;
        step.Category = template.Category;

        var validation = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(step, new(step), validation, validateAllProperties: true))
        {
            error = string.Join(' ', validation.Select(result => result.ErrorMessage));
            return false;
        }

        return true;
    }

    private static JsonObject Properties(DeploymentStep step, JsonSerializerOptions options)
    {
        var node = JsonSerializer.SerializeToNode(step, step.GetType(), options) as JsonObject ?? [];
        foreach (var key in node.Select(pair => pair.Key).Where(key => s_stepBaseProperties.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray())
        {
            node.Remove(key);
        }

        return node;
    }

    private static string? Category(DeploymentStep step) => string.IsNullOrWhiteSpace(step.Category?.Value) ? null : step.Category.Value;

    private static JsonSerializerOptions SerializerOptions(AuthorizedRequest access) =>
        access.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value.SerializerOptions;

    private static bool IsJson(string json, JsonDocumentOptions options)
    {
        try
        {
            JsonNode.Parse(json, null, options);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static bool HasDeployment(AuthorizedRequest access) => access.GetService<IDeploymentManager>() is not null;


    private async Task<AuthorizedRequest?> AuthorizeAllAsync(params Permission[] permissions)
    {
        AuthorizedRequest? access = null;
        foreach (var permission in permissions)
        {
            access = await requestAccess.AuthorizeAsync(User, permission);
            if (access is null) return null;
        }

        return access;
    }

    private async Task<bool> Authorized(Permission permission) => await requestAccess.AuthorizeAsync(User, permission) is not null;

    private IResult Failure(string message) => BadRequest(new DeploymentResult(false, message));
}
