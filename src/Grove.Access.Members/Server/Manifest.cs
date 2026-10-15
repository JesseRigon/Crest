using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Members",
    Author = "Grove",
    Version = "0.0.1",
    Description = "Organization-bound member users: user classes, hierarchies, org bindings, member portals."
)]

[assembly: Feature(
    Id = "Grove.Access.Members",
    Name = "Members",
    Description = "Member user class, per-tenant user hierarchies, org bindings and member portal access control.",
    Dependencies = ["Grove.Access", 
        "Grove.Access.Users",
        "Grove.Access.Roles",
        "Grove.Contents",
        "Grove.Contents.Fields",
        "Grove.Contents.Title",
        "Grove.Contents.PartLists",
        "Grove.Parties"
    ],
    Category = "Business"
)]
