using Crest.Modules.Manifest;

[assembly: Module(
    Author = "Crest (forked from Elsa Workflows, MIT)",
    Website = "https://github.com/JesseRigon/Crest",
    Version = "0.1.0",
    Name = "Crest Workflows"
)]

// The engine. The platform modules' workflow startups (Email, Users, Contents, ...) are
// gated on this feature id and contribute their activities to the platform activity library
// (Crest.Workflows.Abstractions), which this feature hosts and runs through the
// engine (docs/workflows.md). No OpenID: the API rides the tenant cookie behind Crest's
// antiforgery header and the ManageWorkflows permission (Security/ApiSecurityMiddleware).
[assembly: Feature(
    Id = "Crest.Workflows",
    Name = "Crest Workflows",
    Description = "Crest.Workflows 3 as the tenant's workflow engine: definitions as content items, per-shell stores, the Crest.Workflows API behind Crest permissions.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Contents", "Crest.Liquid", "Crest.Scripting", "Crest"]
)]

[assembly: Feature(
    Id = "Crest.Workflows.Http",
    Name = "HTTP Activities",
    Description = "Crest.Workflows HTTP endpoint and request activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows"]
)]
