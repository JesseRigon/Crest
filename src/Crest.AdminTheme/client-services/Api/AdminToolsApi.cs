using System.Net.Http.Json;
using Crest.Validation;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Crest.AdminTheme.Api;

// The admin's tool pages (the SQL runner, the debugging settings, deployments): the client
// calls and the records both sides share (Crest.Server's controllers return these types).

public sealed record SqlRunRequest(string Sql, JsonObject? Parameters);

public sealed record SqlRun(string[] Errors, string? RawSql, JsonObject Parameters, string[] Columns, JsonArray Rows, double ElapsedMilliseconds, string? FactoryName)
{
    public static SqlRun Failed(string message) => new([message], null, [], [], [], 0, null);
}

public sealed record CrestDebugSettings(bool WriteShapeDebugInformation);

public sealed record CrestDeploymentPlan(long Id, string Name, int StepCount);

public sealed record DeploymentPlanName(string Name);

public sealed record DeploymentPlanIds(long[]? Ids);

/// <summary>A step of a plan. <c>Properties</c> is the step's own settings, as the plan stores them.</summary>
public sealed record CrestDeploymentStep(string Id, string Type, string? Category, JsonObject Properties);

/// <summary>A step type the tenant's enabled features offer, with a new step's settings.</summary>
public sealed record DeploymentStepType(string Type, string? Category, JsonObject Defaults);

public sealed record DeploymentStepWrite(string Type, JsonObject? Properties);

public sealed record DeploymentStepMove(int OldIndex, int NewIndex);

public sealed record DeploymentTargetRef(string Id, string Name);

public sealed record DeploymentPlanDetail(long Id, string Name, CrestDeploymentStep[] Steps, DeploymentStepType[] StepTypes, DeploymentTargetRef[] RemoteInstances, bool CanExport);

public sealed record DeploymentResult(bool Succeeded, string? Message);

public sealed record DeploymentJsonImport(string Json);

/// <summary>A remote instance a plan can be sent to. The API key is write-only: it is never read back.</summary>
public sealed record CrestRemoteInstance(string Id, string Name, string Url, string ClientName, bool HasApiKey);

/// <summary>Writes a remote instance; an empty <c>ApiKey</c> on an update keeps the stored one.</summary>
public sealed record RemoteInstanceWrite([property: NotEmpty] string Name, [property: NotEmpty, AbsoluteUrl] string Url, [property: NotEmpty] string ClientName, string? ApiKey);

/// <summary>A client allowed to send packages to this instance. The API key is write-only.</summary>
public sealed record CrestRemoteClient(string Id, string ClientName);

/// <summary>Writes a remote client; an empty <c>ApiKey</c> on an update keeps the stored one.</summary>
public sealed record RemoteClientWrite([property: NotEmpty] string ClientName, string? ApiKey);

public interface IDebugSettingsApi
{
    Task<CrestDebugSettings?> GetAsync();
    Task<CrestDebugSettings?> UpdateAsync(CrestDebugSettings value);
}

public interface IDeploymentApi
{
    Task<CrestDeploymentPlan[]> ListPlansAsync(string? search = null);
    Task<DeploymentResult> CreatePlanAsync(string name);
    Task<DeploymentResult> RenamePlanAsync(long id, string name);
    Task<bool> DeletePlansAsync(long[] ids);
    Task<DeploymentPlanDetail?> GetPlanAsync(long id);
    Task<DeploymentResult> AddStepAsync(long planId, DeploymentStepWrite step);
    Task<DeploymentResult> SaveStepAsync(long planId, string stepId, DeploymentStepWrite step);
    Task<bool> DeleteStepAsync(long planId, string stepId);
    Task<bool> MoveStepAsync(long planId, int oldIndex, int newIndex);
    /// <summary>The plan's package (a zip), or null when it could not be built.</summary>
    Task<(byte[] Content, string FileName)?> ExportAsync(long planId);
    Task<DeploymentResult> SendToRemoteAsync(long planId, string remoteInstanceId);
    Task<DeploymentResult> ImportJsonAsync(string json);
    Task<DeploymentResult> ImportPackageAsync(string fileName, Stream content);
    Task<CrestRemoteInstance[]> ListRemoteInstancesAsync();
    Task<DeploymentResult> SaveRemoteInstanceAsync(string? id, RemoteInstanceWrite value);
    Task<bool> DeleteRemoteInstanceAsync(string id);
    Task<CrestRemoteClient[]> ListRemoteClientsAsync();
    Task<DeploymentResult> SaveRemoteClientAsync(string? id, RemoteClientWrite value);
    Task<bool> DeleteRemoteClientAsync(string id);
}

public sealed class DebugSettingsApi(HttpClient http) : IDebugSettingsApi
{
    public async Task<CrestDebugSettings?> GetAsync()
    {
        using var response = await http.SendAsync(AdminToolsHttp.C(new(HttpMethod.Get, "api/crest/settings/debugging")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestDebugSettings>() : null;
    }

    public async Task<CrestDebugSettings?> UpdateAsync(CrestDebugSettings value)
    {
        using var request = AdminToolsHttp.C(new(HttpMethod.Put, "api/crest/settings/debugging"));
        request.Content = JsonContent.Create(value);
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestDebugSettings>() : null;
    }
}

public sealed class DeploymentApi(HttpClient http) : IDeploymentApi
{
    private const string Base = "api/crest/deployment";

    public async Task<CrestDeploymentPlan[]> ListPlansAsync(string? search = null)
    {
        var uri = string.IsNullOrWhiteSpace(search) ? $"{Base}/plans" : $"{Base}/plans?search={Uri.EscapeDataString(search)}";
        using var response = await http.SendAsync(AdminToolsHttp.C(new(HttpMethod.Get, uri)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestDeploymentPlan[]>() ?? [] : [];
    }

    public Task<DeploymentResult> CreatePlanAsync(string name) => SendAsync(HttpMethod.Post, $"{Base}/plans", new DeploymentPlanName(name));

    public Task<DeploymentResult> RenamePlanAsync(long id, string name) => SendAsync(HttpMethod.Put, $"{Base}/plans/{id}", new DeploymentPlanName(name));

    public async Task<bool> DeletePlansAsync(long[] ids) => (await SendAsync(HttpMethod.Post, $"{Base}/plans/delete", new DeploymentPlanIds(ids))).Succeeded;

    public async Task<DeploymentPlanDetail?> GetPlanAsync(long id)
    {
        using var response = await http.SendAsync(AdminToolsHttp.C(new(HttpMethod.Get, $"{Base}/plans/{id}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<DeploymentPlanDetail>() : null;
    }

    public Task<DeploymentResult> AddStepAsync(long planId, DeploymentStepWrite step) => SendAsync(HttpMethod.Post, $"{Base}/plans/{planId}/steps", step);

    public Task<DeploymentResult> SaveStepAsync(long planId, string stepId, DeploymentStepWrite step) => SendAsync(HttpMethod.Put, $"{Base}/plans/{planId}/steps/{Uri.EscapeDataString(stepId)}", step);

    public async Task<bool> DeleteStepAsync(long planId, string stepId) => (await SendAsync(HttpMethod.Delete, $"{Base}/plans/{planId}/steps/{Uri.EscapeDataString(stepId)}", null)).Succeeded;

    public async Task<bool> MoveStepAsync(long planId, int oldIndex, int newIndex) => (await SendAsync(HttpMethod.Post, $"{Base}/plans/{planId}/steps/move", new DeploymentStepMove(oldIndex, newIndex))).Succeeded;

    public async Task<(byte[] Content, string FileName)?> ExportAsync(long planId)
    {
        using var response = await http.SendAsync(AdminToolsHttp.C(new(HttpMethod.Get, $"{Base}/plans/{planId}/package")));
        if (!response.IsSuccessStatusCode) return null;
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "deployment.zip";
        return (await response.Content.ReadAsByteArrayAsync(), fileName);
    }

    public Task<DeploymentResult> SendToRemoteAsync(long planId, string remoteInstanceId) => SendAsync(HttpMethod.Post, $"{Base}/plans/{planId}/remote/{Uri.EscapeDataString(remoteInstanceId)}", null);

    public Task<DeploymentResult> ImportJsonAsync(string json) => SendAsync(HttpMethod.Post, $"{Base}/import/json", new DeploymentJsonImport(json));

    public async Task<DeploymentResult> ImportPackageAsync(string fileName, Stream content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), "package", fileName);
        using var request = AdminToolsHttp.C(new(HttpMethod.Post, $"{Base}/import/package"));
        request.Content = form;
        using var response = await http.SendAsync(request);
        return await AdminToolsHttp.ResultAsync(response);
    }

    public async Task<CrestRemoteInstance[]> ListRemoteInstancesAsync()
    {
        using var response = await http.SendAsync(AdminToolsHttp.C(new(HttpMethod.Get, $"{Base}/remote-instances")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestRemoteInstance[]>() ?? [] : [];
    }

    public Task<DeploymentResult> SaveRemoteInstanceAsync(string? id, RemoteInstanceWrite value) => id is null
        ? SendAsync(HttpMethod.Post, $"{Base}/remote-instances", value)
        : SendAsync(HttpMethod.Put, $"{Base}/remote-instances/{Uri.EscapeDataString(id)}", value);

    public async Task<bool> DeleteRemoteInstanceAsync(string id) => (await SendAsync(HttpMethod.Delete, $"{Base}/remote-instances/{Uri.EscapeDataString(id)}", null)).Succeeded;

    public async Task<CrestRemoteClient[]> ListRemoteClientsAsync()
    {
        using var response = await http.SendAsync(AdminToolsHttp.C(new(HttpMethod.Get, $"{Base}/remote-clients")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestRemoteClient[]>() ?? [] : [];
    }

    public Task<DeploymentResult> SaveRemoteClientAsync(string? id, RemoteClientWrite value) => id is null
        ? SendAsync(HttpMethod.Post, $"{Base}/remote-clients", value)
        : SendAsync(HttpMethod.Put, $"{Base}/remote-clients/{Uri.EscapeDataString(id)}", value);

    public async Task<bool> DeleteRemoteClientAsync(string id) => (await SendAsync(HttpMethod.Delete, $"{Base}/remote-clients/{Uri.EscapeDataString(id)}", null)).Succeeded;

    private async Task<DeploymentResult> SendAsync(HttpMethod method, string uri, object? body)
    {
        using var request = AdminToolsHttp.C(new(method, uri));
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        using var response = await http.SendAsync(request);
        return await AdminToolsHttp.ResultAsync(response);
    }
}

internal static class AdminToolsHttp
{
    public static HttpRequestMessage C(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }

    /// <summary>A write's outcome: the server's <see cref="DeploymentResult"/>, or its status as a message.</summary>
    public static async Task<DeploymentResult> ResultAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType is "application/json")
        {
            try
            {
                if (await response.Content.ReadFromJsonAsync<DeploymentResult>() is { } result) return result;
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        var text = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        return new(response.IsSuccessStatusCode, string.IsNullOrWhiteSpace(text) ? (response.IsSuccessStatusCode ? null : response.ReasonPhrase) : text);
    }
}
