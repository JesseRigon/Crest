using Grove.Modules.Manifest;

[assembly: Module(
    Author = "Grove (forked from Elsa Workflows, MIT)",
    Website = "https://github.com/JesseRigon/Grove",
    Version = "0.1.0",
    Name = "Grove Workflows"
)]

// The engine. The platform modules' workflow startups (Email, Users, Contents, ...) are
// gated on this feature id and contribute their activities to the platform activity library
// (Grove.Workflows.Abstractions), which this feature hosts and runs through the
// engine (docs/workflows.md). No OpenID: the API rides the tenant cookie behind Grove's
// antiforgery header and the ManageWorkflows permission (Security/ApiSecurityMiddleware).
[assembly: Feature(
    Id = "Grove.Workflows",
    Name = "Grove Workflows",
    Description = "Grove.Workflows 3 as the tenant's workflow engine: definitions as content items, per-shell stores, the Grove.Workflows API behind Grove permissions.",
    Category = "Grove.Workflows",
    Dependencies = ["Grove.Access", "Grove.Contents", "Grove.Display.Liquid", "Grove.Scripting", "Grove"]
)]

[assembly: Feature(
    Id = "Grove.Workflows.Samples",
    Name = "Workflow samples",
    Description = "Fixtures for exercising workflows with no host module: a content-published registry trigger, a system sample flow and a shipped draft template. Off by default.",
    Category = "Samples",
    Dependencies = ["Grove.Workflows"]
)]

[assembly: Feature(
    Id = "Grove.Workflows.Http",
    Name = "HTTP Activities",
    Description = "Grove.Workflows HTTP endpoint and request activities.",
    Category = "Grove.Workflows",
    Dependencies = ["Grove.Workflows"]
)]
