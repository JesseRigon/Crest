using Grove.Themes.Admin.Api;
using Grove.Display.Components.Primitives;
using Microsoft.JSInterop;

namespace Grove.Themes.Admin.Pages;

/// <summary>Downloads a deployment plan's package: fetched through the API client, saved by the browser.</summary>
internal static class DeploymentDownload
{
    public static async Task SaveAsync(IApi api, IJSRuntime js, long planId, NotificationService notifications, Grove.Display.Components.Theme.GroveApiLocalizer T)
    {
        var package = await api.Grove.Rest.Deployment.ExportAsync(planId);
        if (package is not { } file)
        {
            notifications.Notify(NotificationSeverity.Error, T["Download package"], T["The package could not be built."]);
            return;
        }

        using var stream = new MemoryStream(file.Content);
        using var reference = new DotNetStreamReference(stream);
        await js.InvokeVoidAsync("Grove.downloadFile", file.FileName, reference, "application/zip");
    }
}
