using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Sharing",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Share any module's items with users, roles, organizations, the tenant or the public: grants and blocks that flow down a tree, and ordered rules. Decided by the access gate.",
    Category = "Security",
    Dependencies = ["Grove.Access"]
)]
