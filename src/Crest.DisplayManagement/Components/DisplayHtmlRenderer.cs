#nullable enable

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The HTML path of the display system (§ 8, rework step 6): a built tree rendered to static HTML
/// by <see cref="HtmlRenderer"/> and the same <see cref="DisplayTreeRenderer"/> the browser runs,
/// for email bodies, PDF sources and cached static pages. No interaction: events and bound slots
/// have no browser to run in.
/// </summary>
public interface IDisplayHtmlRenderer
{
    Task<string> RenderAsync(DisplayNode root, CancellationToken cancellationToken = default);
}

public sealed class DisplayHtmlRenderer(IServiceProvider services, ILoggerFactory loggerFactory) : IDisplayHtmlRenderer
{
    public async Task<string> RenderAsync(DisplayNode root, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var renderer = new HtmlRenderer(services, loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DisplayTreeRenderer.Node)] = root,
            });

            var output = await renderer.RenderComponentAsync<DisplayTreeRenderer>(parameters);
            return output.ToHtmlString();
        });
    }
}
