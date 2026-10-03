using OrchardCore.Modules.Manifest;

[assembly: Module(
    Author = "OrchardCore.Crest (forked from Elsa Workflows, MIT)",
    Website = "https://github.com/JesseRigon/OrchardCore.Crest",
    Version = "0.1.0",
    Name = "Crest Workflows"
)]

// The engine. Depends on the stock OrchardCore.Workflows feature on purpose: the upstream
// modules' workflow startups (Email, Users, Contents, ...) are gated on that feature id,
// and the override route (plans/workflows.md) keeps them running while Crest replaces the
// services behind them. No OpenID: the API rides the tenant cookie behind Crest's
// antiforgery header and the ManageWorkflows permission (Security/CrestWorkflowsApiSecurityMiddleware).
[assembly: Feature(
    Id = "Crest.Workflows",
    Name = "Crest Workflows",
    Description = "Crest.Workflows 3 as the tenant's workflow engine: definitions as content items, per-shell stores, the Crest.Workflows API behind Orchard permissions.",
    Category = "Crest.Workflows",
    Dependencies = ["OrchardCore.Contents", "OrchardCore.Workflows", "OrchardCore.Crest"]
)]

[assembly: Feature(
    Id = "Crest.Workflows.Http",
    Name = "HTTP Activities",
    Description = "Crest.Workflows HTTP endpoint and request activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows"]
)]
