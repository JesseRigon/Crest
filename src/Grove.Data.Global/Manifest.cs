using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Global Reference Data",
    Author = "Grove",
    Version = "1.0.0",
    Category = "Grove"
)]

// The permission that gates super-tenant editing and, later, the generated editing UI, and
// the tenant-less store itself: one per process (GlobalHost), handed to every tenant
// that enables this feature, with no host call.
[assembly: Feature(
    Id = "Grove.Data.Global",
    Name = "Grove Global Reference Data",
    Description = "Reference data shared by every tenant, editable only from the Default tenant.",
    Category = "Grove",
    Dependencies = ["Grove"]
)]
