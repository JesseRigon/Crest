using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Contents",
    Author = "Crest (forked from Elsa Workflows, MIT)",
    Website = "https://github.com/JesseRigon/Crest",
    Version = "0.1.0"
)]

[assembly: Feature(
    Id = "Crest.Workflows.Contents",
    Name = "Content Activities",
    Description = "Content triggers (created, published, ...) carrying the acting user, and content tasks.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows", "OrchardCore.Contents", "OrchardCore.Title"]
)]
