using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Members",
    Author = "OrchardCore.Crest",
    Version = "0.0.1",
    Description = "Organization-bound member users: user classes, hierarchies, org bindings, member portals."
)]

[assembly: Feature(
    Id = "Crest.Members",
    Name = "Members",
    Description = "Member user class, per-tenant user hierarchies, org bindings and member portal access control.",
    Dependencies = [
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "OrchardCore.Contents",
        "OrchardCore.ContentFields",
        "OrchardCore.Title",
        "Crest.ContentPartLists",
        "Crest.Parties"
    ],
    Category = "Business"
)]
