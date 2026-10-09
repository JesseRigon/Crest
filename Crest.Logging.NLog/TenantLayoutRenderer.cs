using System.Text;
using NLog;
using NLog.LayoutRenderers;
using NLog.Web.LayoutRenderers;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;

namespace Crest.Logging;

/// <summary>
/// Print the tenant name.
/// </summary>
[LayoutRenderer(LayoutRendererName)]
public class TenantLayoutRenderer : AspNetLayoutRendererBase
{
    public const string LayoutRendererName = "platform-tenant-name";

    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
    {
        // If there is no ShellContext in the Features then the log is rendered from the Host.
        var tenantName = HttpContextAccessor?.HttpContext?.Features.Get<ShellContextFeature>()
            ?.ShellContext.Settings.Name;

        tenantName ??= ShellScope.Context?.Settings.Name;

        builder.Append(tenantName ?? "None");
    }
}
