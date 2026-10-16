using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Workflows Contents",
    Author = "Grove (forked from Elsa Workflows, MIT)",
    Website = "https://github.com/JesseRigon/Grove",
    Version = "0.1.0"
)]

[assembly: Feature(
    Id = "Grove.Workflows.Contents",
    Name = "Content Activities",
    Description = "The content lifecycle's hooks and triggers (created, published, ...) carrying the acting user, and content tasks.",
    Category = "Grove.Workflows",
    Dependencies = ["Grove.Access", "Grove.Workflows", "Grove.Contents"],
    // The field sets' system flows run on the content lifecycle's hooks (docs/content-engine.md ›
    // Behaviour is workflows), so it is never off.
    IsAlwaysEnabled = true
)]
