using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Global Reference Data",
    Author = "Crest",
    Version = "1.0.0",
    Category = "Crest"
)]

// The permission that gates super-tenant editing and, later, the generated editing UI, and
// the tenant-less store itself: one per process (GlobalHost), handed to every tenant
// that enables this feature, with no host call.
[assembly: Feature(
    Id = "Crest.Data.Global",
    Name = "Crest Global Reference Data",
    Description = "Reference data shared by every tenant, editable only from the Default tenant.",
    Category = "Crest",
    Dependencies = ["Crest"]
)]
