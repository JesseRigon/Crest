using Crest.Security.Permissions;

namespace Crest.Contents.VersionPruning;

public static class ContentVersionPruningPermissions
{
    public static readonly Permission ManageContentVersionPruningSettings = new(
        "ManageContentVersionPruningSettings",
        "Manage Content Version Pruning settings");
}
