using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest.Entities;
using Crest.Environment.Shell;
using Crest.Security;
using Crest.Security.Options;
using Crest.Security.Settings;
using Crest.Settings;

namespace Crest.ViewModels;

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
