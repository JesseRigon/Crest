using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Members",
    Author = "Grove",
    Version = "0.0.1",
    Description = "Organization-bound member users: user classes, hierarchies, org bindings, member portals."
)]

[assembly: Feature(
    Id = "Grove.Members",
    Name = "Members",
    Description = "Member user class, per-tenant user hierarchies, org bindings and member portal access control.",
    Dependencies = ["Grove.Access", 
        "Grove.Users",
        "Grove.Roles",
        "Grove.Contents",
        "Grove.ContentFields",
        "Grove.Title",
        "Grove.Contents.PartLists",
        "Grove.Parties"
    ],
    Category = "Business"
)]
