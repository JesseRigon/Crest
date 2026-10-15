using Grove.Security.Permissions;

namespace Grove.Permissions;

/// <summary>
/// The option sources API's gate: querying, resolving and validating picker rows. Each source
/// still decides which rows the caller may see (a record the caller may not see is absent).
/// </summary>
public sealed class OptionPickerPermissions : IPermissionProvider
{
    public static readonly Permission QueryOptionSources = new(nameof(QueryOptionSources), "Query option sources in pickers");

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>([QueryOptionSources]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new() { Name = "Administrator", Permissions = [QueryOptionSources] },
        new() { Name = "Editor", Permissions = [QueryOptionSources] },
        new() { Name = "Author", Permissions = [QueryOptionSources] },
        new() { Name = "Contributor", Permissions = [QueryOptionSources] },
    ];
}
