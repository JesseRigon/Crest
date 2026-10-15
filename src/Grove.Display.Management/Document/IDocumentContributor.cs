#nullable enable

using System.Text;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Management.Document;

/// <summary>
/// The head contribution seam (docs/MVC/views.md › Themes, layouts and layout filters): a module
/// adds markup to the shell documents (<c>App.razor</c>) it serves, in the head or at the end of
/// the body: analytics and pixel scripts, the profiler's includes. Contributors run once per
/// server-rendered document, for the shell the request is in; register with
/// <see cref="DocumentContributorServiceCollectionExtensions.AddDocumentContributor{T}"/>.
/// The markup is trusted (it is the module's own, with values it encoded); it is not sanitized.
/// </summary>
public interface IDocumentContributor
{
    ValueTask ContributeAsync(DocumentContext context);
}

/// <summary>Where a contribution goes in the shell document.</summary>
public enum DocumentSlot
{
    /// <summary>Inside <c>&lt;head&gt;</c>, after the shell's own links.</summary>
    Head,

    /// <summary>At the end of <c>&lt;body&gt;</c>, before the Blazor script.</summary>
    BodyEnd,
}

/// <summary>One shell document being rendered, and what the contributors added to it.</summary>
public sealed class DocumentContext(HttpContext httpContext, ShellKind shell)
{
    private readonly List<(DocumentSlot Slot, int Order, string Markup)> _contributions = [];

    public HttpContext HttpContext { get; } = httpContext;

    /// <summary>The shell the document belongs to (site, admin, member).</summary>
    public ShellKind Shell { get; } = shell;

    /// <summary>True when the visitor allows tracking (no consent feature means allowed).</summary>
    public bool CanTrack => HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ITrackingConsentFeature>()?.CanTrack ?? true;

    public DocumentContext Add(DocumentSlot slot, string markup, int order = 0)
    {
        if (!string.IsNullOrEmpty(markup))
        {
            _contributions.Add((slot, order, markup));
        }

        return this;
    }

    /// <summary>The markup of a slot, contributions ordered by <c>order</c> then by registration.</summary>
    public string Markup(DocumentSlot slot)
    {
        var builder = new StringBuilder();
        foreach (var contribution in _contributions.Where(entry => entry.Slot == slot).OrderBy(entry => entry.Order))
        {
            builder.Append(contribution.Markup).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Runs every registered contributor for a document of <paramref name="shell"/>.</summary>
    public static async Task<DocumentContext> BuildAsync(HttpContext httpContext, ShellKind shell)
    {
        var context = new DocumentContext(httpContext, shell);
        foreach (var contributor in httpContext.RequestServices.GetServices<IDocumentContributor>())
        {
            await contributor.ContributeAsync(context);
        }

        return context;
    }
}

public static class DocumentContributorServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IDocumentContributor
    {
        services.AddScoped<IDocumentContributor, TContributor>();
        return services;
    }
}
