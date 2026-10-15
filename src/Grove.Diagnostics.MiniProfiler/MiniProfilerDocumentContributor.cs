using Microsoft.AspNetCore.Authorization;
using Grove.Display.Management.Components;
using Grove.Display.Management.Document;
using StackExchange.Profiling;

namespace Grove.Diagnostics.MiniProfiler;

/// <summary>
/// The profiler's includes at the end of the documents: the site's for a viewer with
/// <c>ViewMiniProfilerOnFrontEnd</c>, the admin's and the member area's with <c>ViewMiniProfilerOnBackEnd</c>.
/// </summary>
public sealed class MiniProfilerDocumentContributor(IAuthorizationService authorizationService) : IDocumentContributor
{
    public async ValueTask ContributeAsync(DocumentContext context)
    {
        var permission = context.Shell == ShellKind.Site ? Permissions.ViewMiniProfilerOnFrontEnd : Permissions.ViewMiniProfilerOnBackEnd;
        if (!await authorizationService.AuthorizeAsync(context.HttpContext.User, permission))
        {
            return;
        }

        if (StackExchange.Profiling.MiniProfiler.Current is { } profiler)
        {
            context.Add(DocumentSlot.BodyEnd, profiler.RenderIncludes(context.HttpContext).ToString());
        }
    }
}
