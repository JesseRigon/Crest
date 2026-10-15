using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Extensions.Features;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;

namespace Grove.Media.Events;

internal sealed class SecureMediaFeatureEventHandler : FeatureEventHandler
{
    public override Task EnablingAsync(IFeatureInfo feature)
    {
        if (feature.Id == "Grove.Media.Security")
        {
            return NotifyMediaSecurityFeatureEnabledAsync();
        }

        return base.EnablingAsync(feature);
    }

    // The roles decide who may see secure media: the enabling is logged as a warning to review them.
    private Task NotifyMediaSecurityFeatureEnabledAsync()
    {
        ShellScope.Services.GetService<Microsoft.Extensions.Logging.ILogger<SecureMediaFeatureEventHandler>>()?.LogWarning(
            "Secure Media is now enabled. Review and update the role permissions so users can access only the media they should.");

        return Task.CompletedTask;
    }
}
