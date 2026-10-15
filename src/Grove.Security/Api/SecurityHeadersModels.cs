#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Grove.Entities;
using Grove.Environment.Shell;
using Grove.Security;
using Grove.Security.Options;
using Grove.Security.Settings;
using Grove.Settings;

namespace Grove.Security.Api;

public sealed record SecurityHeadersDto(
    Dictionary<string, string>? ContentSecurityPolicy,
    Dictionary<string, string>? PermissionsPolicy,
    string? ReferrerPolicy,
    bool FromConfiguration)
{
    public static SecurityHeadersDto From(SecuritySettings settings, bool fromConfiguration) => new(
        settings.ContentSecurityPolicy,
        settings.PermissionsPolicy,
        settings.ReferrerPolicy,
        fromConfiguration);
}
