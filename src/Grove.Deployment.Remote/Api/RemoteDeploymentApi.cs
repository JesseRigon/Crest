#nullable enable

using System.Net.Http.Headers;
using Grove.Api;
using Grove.Validation;
using Grove.Contents.Management.Utilities;
using Grove.Deployment.Api;
using Grove.Deployment.Remote.Services;
using Grove.Deployment.Services;
using Grove.Data.Storage;
using Grove.Infrastructure.Recipes.Models;
using Grove.Security.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;
using ISession = YesSql.ISession;

namespace Grove.Deployment.Remote.Api;

/// <summary>
/// Remote deployment (<c>api/grove/deployment</c>): sending a plan to a remote instance, and the
/// remote instances and clients. The plans themselves are Grove.Deployment's API.
/// </summary>
public sealed class RemoteDeploymentApi : ApiContributor
{
    public override string Name => "RemoteDeployment";

    public override string FeatureId => "Grove.Deployment.Remote";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("deployment");
        routes.MapPost("plans/{id:long}/remote/{remoteInstanceId}", (HttpContext __http, [FromRoute] long id, [FromRoute] string remoteInstanceId) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).SendToRemoteAsync(id, remoteInstanceId));
        routes.MapGet("remote-instances", ApiHandlers.Handle<RemoteDeploymentHandlers>(h => h.ListRemoteInstancesAsync()));
        routes.MapPost("remote-instances", (HttpContext __http, [FromBody] RemoteInstanceWrite request) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).CreateRemoteInstanceAsync(request));
        routes.MapPut("remote-instances/{id}", (HttpContext __http, [FromRoute] string id, [FromBody] RemoteInstanceWrite request) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).UpdateRemoteInstanceAsync(id, request));
        routes.MapDelete("remote-instances/{id}", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).DeleteRemoteInstanceAsync(id));
        routes.MapGet("remote-clients", ApiHandlers.Handle<RemoteDeploymentHandlers>(h => h.ListRemoteClientsAsync()));
        routes.MapPost("remote-clients", (HttpContext __http, [FromBody] RemoteClientWrite request) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).CreateRemoteClientAsync(request));
        routes.MapPut("remote-clients/{id}", (HttpContext __http, [FromRoute] string id, [FromBody] RemoteClientWrite request) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).UpdateRemoteClientAsync(id, request));
        routes.MapDelete("remote-clients/{id}", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<RemoteDeploymentHandlers>(__http).DeleteRemoteClientAsync(id));
    }
}

public sealed class RemoteDeploymentHandlers(IRequestAccess requestAccess, ILogger<RemoteDeploymentHandlers> logger) : ApiHandlers
{

    public async Task<IResult> SendToRemoteAsync(long id, string remoteInstanceId)
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


    public async Task<IResult> ListRemoteInstancesAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteInstances);
        if (access is null) return Forbid();
        if (access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();

        return Ok((await remote.GetRemoteInstanceListAsync()).RemoteInstances
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .Select(instance => new RemoteInstanceModel(instance.Id, instance.Name, instance.Url, instance.ClientName, !string.IsNullOrEmpty(instance.ApiKey)))
            .ToArray());
    }


    public Task<IResult> CreateRemoteInstanceAsync([FromBody] RemoteInstanceWrite request) => SaveRemoteInstanceAsync(null, request);


    public Task<IResult> UpdateRemoteInstanceAsync(string id, [FromBody] RemoteInstanceWrite request) => SaveRemoteInstanceAsync(id, request);


    public async Task<IResult> DeleteRemoteInstanceAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteInstances);
        if (access is null) return Forbid();
        if (access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();
        if (await remote.GetRemoteInstanceAsync(id) is null) return NotFound();

        await remote.DeleteRemoteInstanceAsync(id);
        return Ok(new DeploymentResult(true, null));
    }


    public async Task<IResult> ListRemoteClientsAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteClients);
        if (access is null) return Forbid();
        if (access.GetService<RemoteClientService>() is not { } clients) return NotFound();

        return Ok((await clients.GetRemoteClientListAsync()).RemoteClients
            .OrderBy(client => client.ClientName, StringComparer.OrdinalIgnoreCase)
            .Select(client => new RemoteClientModel(client.Id, client.ClientName))
            .ToArray());
    }


    public Task<IResult> CreateRemoteClientAsync([FromBody] RemoteClientWrite request) => SaveRemoteClientAsync(null, request);


    public Task<IResult> UpdateRemoteClientAsync(string id, [FromBody] RemoteClientWrite request) => SaveRemoteClientAsync(id, request);


    public async Task<IResult> DeleteRemoteClientAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteClients);
        if (access is null) return Forbid();
        if (access.GetService<RemoteClientService>() is not { } clients) return NotFound();
        if (await clients.GetRemoteClientAsync(id) is null) return NotFound();

        await clients.DeleteRemoteClientAsync(id);
        return Ok(new DeploymentResult(true, null));
    }


    private async Task<IResult> SaveRemoteInstanceAsync(string? id, RemoteInstanceWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteInstances);
        if (access is null) return Forbid();
        if (access.GetService<RemoteInstanceService>() is not { } remote) return NotFound();

        var existing = id is null ? null : await remote.GetRemoteInstanceAsync(id);
        if (id is not null && existing is null) return NotFound();

        var apiKey = string.IsNullOrEmpty(request.ApiKey) ? existing?.ApiKey : request.ApiKey;
        if (string.IsNullOrEmpty(apiKey)) return Failure("The api key is mandatory.");

        if (existing is null) await remote.CreateRemoteInstanceAsync(request.Name.Trim(), request.Url.Trim(), request.ClientName.Trim(), apiKey);
        else await remote.UpdateRemoteInstance(existing.Id, request.Name.Trim(), request.Url.Trim(), request.ClientName.Trim(), apiKey);

        return Ok(new DeploymentResult(true, null));
    }


    private async Task<IResult> SaveRemoteClientAsync(string? id, RemoteClientWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, DeploymentPermissions.ManageRemoteClients);
        if (access is null) return Forbid();
        if (access.GetService<RemoteClientService>() is not { } clients) return NotFound();

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

    private static IResult Failure(string message) => BadRequest(new DeploymentResult(false, message));
}

/// <summary>The remote instances a plan can be sent to, for the plan's page.</summary>
internal sealed class RemoteInstanceTargets(RemoteInstanceService remote) : IRemoteDeploymentTargets
{
    public async Task<IReadOnlyList<DeploymentTargetRef>> ListAsync() =>
        (await remote.GetRemoteInstanceListAsync()).RemoteInstances
            .Select(instance => new DeploymentTargetRef(instance.Id, instance.Name))
            .ToArray();
}

public sealed record RemoteInstanceModel(string Id, string Name, string Url, string ClientName, bool HasApiKey);

public sealed record RemoteInstanceWrite([property: NotEmpty] string Name, [property: NotEmpty, AbsoluteUrl] string Url, [property: NotEmpty] string ClientName, string? ApiKey);

public sealed record RemoteClientModel(string Id, string ClientName);

public sealed record RemoteClientWrite([property: NotEmpty] string ClientName, string? ApiKey);
