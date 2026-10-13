using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Deployment.Indexes;
using Crest.Deployment.Remote.Services;
using Crest.Deployment.Services;
using Crest.FileStorage;
using Crest.Json;
using Crest.Mvc.Utilities;
using Crest.Recipes.Models;
using Crest.Security.Permissions;
using Crest.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Services;
using DeploymentJsonImport = Crest.AdminTheme.Api.DeploymentJsonImport;
using CrestDeploymentPlan = Crest.AdminTheme.Api.CrestDeploymentPlan;
using DeploymentPlanDetail = Crest.AdminTheme.Api.DeploymentPlanDetail;
using DeploymentPlanIds = Crest.AdminTheme.Api.DeploymentPlanIds;
using DeploymentPlanName = Crest.AdminTheme.Api.DeploymentPlanName;
using DeploymentResult = Crest.AdminTheme.Api.DeploymentResult;
using CrestDeploymentStep = Crest.AdminTheme.Api.CrestDeploymentStep;
using DeploymentStepMove = Crest.AdminTheme.Api.DeploymentStepMove;
using DeploymentStepType = Crest.AdminTheme.Api.DeploymentStepType;
using DeploymentStepWrite = Crest.AdminTheme.Api.DeploymentStepWrite;
using DeploymentTargetRef = Crest.AdminTheme.Api.DeploymentTargetRef;
using CrestRemoteClient = Crest.AdminTheme.Api.CrestRemoteClient;
using RemoteClientWrite = Crest.AdminTheme.Api.RemoteClientWrite;
using CrestRemoteInstance = Crest.AdminTheme.Api.CrestRemoteInstance;
using RemoteInstanceWrite = Crest.AdminTheme.Api.RemoteInstanceWrite;

namespace Crest.Controllers;

/// <summary>
/// The Deployments pages' API: plans and their steps, exporting a plan as a package or to a
/// remote instance, importing a package or a recipe, and the remote instances and clients.
/// The deployment features own the data; when one is disabled its endpoints answer 404.
/// A step's settings travel as the JSON the plan stores them as, so a step type a module adds
/// is editable without a component of its own.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/deployment")]
public sealed class DeploymentController(IRequestAccess requestAccess, ILogger<DeploymentController> logger) : ControllerBase
{
    private static readonly string[] s_stepBaseProperties = [nameof(DeploymentStep.Id), nameof(DeploymentStep.Name), nameof(DeploymentStep.Category)];

    // Plans

    [HttpGet("plans")]
    public async Task<ActionResult<CrestDeploymentPlan[]>> ListPlansAsync([FromQuery] string? search = null)
    {
        var access = await AuthorizeAllAsync(DeploymentPermissions.ManageDeploymentPlan, DeploymentPermissions.Export);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var query = access.GetRequiredService<ISession>().Query<DeploymentPlan, DeploymentPlanIndex>();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(index => index.Name.Contains(search));

        var plans = await query.OrderBy(index => index.Name).ListAsync();
        return Ok(plans.Select(plan => new CrestDeploymentPlan(plan.Id, plan.Name, plan.DeploymentSteps.Count)).ToArray());
    }

    [HttpPost("plans")]
    public async Task<ActionResult<DeploymentResult>> CreatePlanAsync([FromBody] DeploymentPlanName request)
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

    [HttpPut("plans/{id:long}")]
    public async Task<ActionResult<DeploymentResult>> RenamePlanAsync(long id, [FromBody] DeploymentPlanName request)
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

    [HttpPost("plans/delete")]
    public async Task<ActionResult<DeploymentResult>> DeletePlansAsync([FromBody] DeploymentPlanIds request)
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

    [HttpGet("plans/{id:long}")]
    public async Task<ActionResult<DeploymentPlanDetail>> GetPlanAsync(long id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageDeploymentPlan);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();

        var plan = await access.GetRequiredService<ISession>().GetAsync<DeploymentPlan>(id);
        if (plan is null) return NotFound();

        var options = SerializerOptions(access);
        var steps = plan.DeploymentSteps.Select(step => new CrestDeploymentStep(step.Id, step.Name ?? step.GetType().Name, Category(step), Properties(step, options))).ToArray();
        var stepTypes = access.GetRequiredService<IEnumerable<IDeploymentStepFactory>>()
            .Select(factory => (factory.Name, Step: factory.Create()))
            .Select(entry => new DeploymentStepType(entry.Name, Category(entry.Step), Properties(entry.Step, options)))
            .OrderBy(type => type.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(type => type.Type, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var remoteInstances = Array.Empty<DeploymentTargetRef>();
        if (access.GetService<RemoteInstanceService>() is { } remote && await Authorized(DeploymentPermissions.ExportRemoteInstances))
        {
            remoteInstances = (await remote.GetRemoteInstanceListAsync()).RemoteInstances
                .Select(instance => new DeploymentTargetRef(instance.Id, instance.Name))
                .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Ok(new DeploymentPlanDetail(plan.Id, plan.Name, steps, stepTypes, remoteInstances, await Authorized(DeploymentPermissions.Export)));
    }

    // Steps

    [HttpPost("plans/{id:long}/steps")]
    public async Task<ActionResult<DeploymentResult>> AddStepAsync(long id, [FromBody] DeploymentStepWrite request)
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

    [HttpPut("plans/{id:long}/steps/{stepId}")]
    public async Task<ActionResult<DeploymentResult>> SaveStepAsync(long id, string stepId, [FromBody] DeploymentStepWrite request)
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

    [HttpDelete("plans/{id:long}/steps/{stepId}")]
    public async Task<ActionResult<DeploymentResult>> DeleteStepAsync(long id, string stepId)
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

    [HttpPost("plans/{id:long}/steps/move")]
    public async Task<ActionResult<DeploymentResult>> MoveStepAsync(long id, [FromBody] DeploymentStepMove request)
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

    [HttpGet("plans/{id:long}/package")]
    public async Task<IActionResult> ExportAsync(long id)
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

    [HttpPost("plans/{id:long}/remote/{remoteInstanceId}")]
    public async Task<ActionResult<DeploymentResult>> SendToRemoteAsync(long id, string remoteInstanceId)
    {
        var access = await AuthorizeAllAsync(DeploymentPermissions.Export, DeploymentPermissions.ExportRemoteInstances);
        if (access is null) return Forbid();
        if (!HasDeployment(access) || access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();

        var plan = await access.GetRequiredService<ISession>().GetAsync<DeploymentPlan>(id);
        var instance = await remote.GetRemoteInstanceAsync(remoteInstanceId);
        if (plan is null || instance is null) return NotFound();

        var tempDirectory = access.GetRequiredService<ITempDirectoryProvider>();
        var tempRoot = tempDirectory.GetRootDirectory();
        string archive;

        using (var fileBuilder = new TemporaryFileBuilder(tempRoot))
        {
            archive = Path.Combine(tempRoot, Guid.NewGuid().ToString("n") + ".zip");
            await access.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, new DeploymentPlanResult(fileBuilder, new RecipeDescriptor()));
            System.IO.Compression.ZipFile.CreateFromDirectory(fileBuilder.Folder, archive);
        }

        try
        {
            await using var stream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var content = new MultipartFormDataContent
            {
                { new StreamContent(stream), "Content", plan.Name.ToSafeName() + ".zip" },
                { new StringContent(instance.ClientName ?? string.Empty), "ClientName" },
            };

            // The remote deployment key is the call's credential, authenticated by the
            // receiving instance's gate; it never travels in the form.
            var client = access.GetRequiredService<IHttpClientFactory>().CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(RemoteDeploymentKeyAuthenticationHandler.AuthorizationScheme, $"{instance.ClientName}:{instance.ApiKey}");

            using var response = await client.PostAsync(instance.Url, content, HttpContext.RequestAborted);
            return Ok(response.IsSuccessStatusCode
                ? new DeploymentResult(true, "Deployment executed successfully.")
                : new DeploymentResult(false, $"The remote instance answered \"{response.ReasonPhrase} ({(int)response.StatusCode})\"."));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Sending deployment plan {Plan} to remote instance {Instance} failed.", plan.Name, instance.Name);
            return Ok(new DeploymentResult(false, $"The remote instance could not be reached: {exception.Message}"));
        }
        finally
        {
            System.IO.File.Delete(archive);
        }
    }

    // Import

    [HttpPost("import/json")]
    public async Task<ActionResult<DeploymentResult>> ImportJsonAsync([FromBody] DeploymentJsonImport request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.Import);
        if (access is null) return Forbid();
        if (!HasDeployment(access)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Json) || !request.Json.IsJson(JOptions.Document)) return Failure("The recipe is written in an incorrect JSON format.");

        return await ImportAsync(() => DeploymentPackages.ImportJsonAsync(access.GetRequiredService<IDeploymentManager>(), access.GetRequiredService<ITempDirectoryProvider>(), request.Json), "Recipe imported successfully.");
    }

    [HttpPost("import/package")]
    [RequestSizeLimit(256L * 1024 * 1024)]
    public async Task<ActionResult<DeploymentResult>> ImportPackageAsync(IFormFile? package)
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

    // Remote instances

    [HttpGet("remote-instances")]
    public async Task<ActionResult<CrestRemoteInstance[]>> ListRemoteInstancesAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteInstances);
        if (access is null) return Forbid();
        if (access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();

        return Ok((await remote.GetRemoteInstanceListAsync()).RemoteInstances
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .Select(instance => new CrestRemoteInstance(instance.Id, instance.Name, instance.Url, instance.ClientName, !string.IsNullOrEmpty(instance.ApiKey)))
            .ToArray());
    }

    [HttpPost("remote-instances")]
    public Task<ActionResult<DeploymentResult>> CreateRemoteInstanceAsync([FromBody] RemoteInstanceWrite request) => SaveRemoteInstanceAsync(null, request);

    [HttpPut("remote-instances/{id}")]
    public Task<ActionResult<DeploymentResult>> UpdateRemoteInstanceAsync(string id, [FromBody] RemoteInstanceWrite request) => SaveRemoteInstanceAsync(id, request);

    [HttpDelete("remote-instances/{id}")]
    public async Task<ActionResult<DeploymentResult>> DeleteRemoteInstanceAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteInstances);
        if (access is null) return Forbid();
        if (access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();
        if (await remote.GetRemoteInstanceAsync(id) is null) return NotFound();

        await remote.DeleteRemoteInstanceAsync(id);
        return Ok(new DeploymentResult(true, null));
    }

    // Remote clients

    [HttpGet("remote-clients")]
    public async Task<ActionResult<CrestRemoteClient[]>> ListRemoteClientsAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteClients);
        if (access is null) return Forbid();
        if (access.GetService<RemoteClientService>() is not { } clients) return NotFound();

        return Ok((await clients.GetRemoteClientListAsync()).RemoteClients
            .OrderBy(client => client.ClientName, StringComparer.OrdinalIgnoreCase)
            .Select(client => new CrestRemoteClient(client.Id, client.ClientName))
            .ToArray());
    }

    [HttpPost("remote-clients")]
    public Task<ActionResult<DeploymentResult>> CreateRemoteClientAsync([FromBody] RemoteClientWrite request) => SaveRemoteClientAsync(null, request);

    [HttpPut("remote-clients/{id}")]
    public Task<ActionResult<DeploymentResult>> UpdateRemoteClientAsync(string id, [FromBody] RemoteClientWrite request) => SaveRemoteClientAsync(id, request);

    [HttpDelete("remote-clients/{id}")]
    public async Task<ActionResult<DeploymentResult>> DeleteRemoteClientAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteClients);
        if (access is null) return Forbid();
        if (access.GetService<RemoteClientService>() is not { } clients) return NotFound();
        if (await clients.GetRemoteClientAsync(id) is null) return NotFound();

        await clients.DeleteRemoteClientAsync(id);
        return Ok(new DeploymentResult(true, null));
    }

    private async Task<ActionResult<DeploymentResult>> SaveRemoteInstanceAsync(string? id, RemoteInstanceWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteInstances);
        if (access is null) return Forbid();
        if (access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();

        var existing = id is null ? null : await remote.GetRemoteInstanceAsync(id);
        if (id is not null && existing is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name)) return Failure("The name is mandatory.");
        if (string.IsNullOrWhiteSpace(request.ClientName)) return Failure("The client name is mandatory.");
        if (string.IsNullOrWhiteSpace(request.Url)) return Failure("The url is mandatory.");
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _)) return Failure("The url is invalid.");

        var apiKey = string.IsNullOrEmpty(request.ApiKey) ? existing?.ApiKey : request.ApiKey;
        if (string.IsNullOrEmpty(apiKey)) return Failure("The api key is mandatory.");

        if (existing is null) await remote.CreateRemoteInstanceAsync(request.Name.Trim(), request.Url.Trim(), request.ClientName.Trim(), apiKey);
        else await remote.UpdateRemoteInstance(existing.Id, request.Name.Trim(), request.Url.Trim(), request.ClientName.Trim(), apiKey);

        return Ok(new DeploymentResult(true, null));
    }

    private async Task<ActionResult<DeploymentResult>> SaveRemoteClientAsync(string? id, RemoteClientWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteClients);
        if (access is null) return Forbid();
        if (access.GetService<RemoteClientService>() is not { } clients) return NotFound();

        if (string.IsNullOrWhiteSpace(request.ClientName)) return Failure("The client name is mandatory.");

        if (id is null)
        {
            if (string.IsNullOrEmpty(request.ApiKey)) return Failure("The api key is mandatory.");
            await clients.CreateRemoteClientAsync(request.ClientName.Trim(), request.ApiKey);
            return Ok(new DeploymentResult(true, null));
        }

        return await clients.TryUpdateRemoteClient(id, request.ClientName.Trim(), request.ApiKey)
            ? Ok(new DeploymentResult(true, null))
            : NotFound();
    }

    private async Task<ActionResult<DeploymentResult>> ImportAsync(Func<Task> import, string success)
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

    private ActionResult<DeploymentResult> Failure(string message) => BadRequest(new DeploymentResult(false, message));
}
