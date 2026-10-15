using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.HttpOverrides;

namespace Grove.Infrastructure.Server.ReverseProxy.Settings;

/// <summary>
/// The reverse proxy settings form: one switch per forwarded header of
/// <see cref="ReverseProxySettings.ForwardedHeaders"/>.
/// </summary>
public sealed class ForwardedHeadersModel
{
    public const string GroupId = "ReverseProxy";

    [Display(Name = "X-Forwarded-For", Description = "Enables the forwarding of the HTTP header X-Forwarded-For", Order = 0)]
    public bool EnableXForwardedFor { get; set; }

    [Display(Name = "X-Forwarded-Proto", Description = "Enables the forwarding of the HTTP header X-Forwarded-Proto", Order = 1)]
    public bool EnableXForwardedProto { get; set; }

    [Display(Name = "X-Forwarded-Host", Description = "Enables the forwarding of the HTTP header X-Forwarded-Host", Order = 2)]
    public bool EnableXForwardedHost { get; set; }

    public static ForwardedHeadersModel From(ReverseProxySettings settings) => new()
    {
        EnableXForwardedFor = settings.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor),
        EnableXForwardedProto = settings.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto),
        EnableXForwardedHost = settings.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost),
    };

    public void ApplyTo(ReverseProxySettings settings)
    {
        settings.ForwardedHeaders = ForwardedHeaders.None;

        if (EnableXForwardedFor)
        {
            settings.ForwardedHeaders |= ForwardedHeaders.XForwardedFor;
        }

        if (EnableXForwardedHost)
        {
            settings.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;
        }

        if (EnableXForwardedProto)
        {
            settings.ForwardedHeaders |= ForwardedHeaders.XForwardedProto;
        }
    }
}
