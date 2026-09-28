using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Contents",
    Author = "Fruitful (forked from Elsa Workflows' Orchard Core integration, BSD-3-Clause)",
    Website = "https://github.com/JesseRigon/OrchardCore.Crest",
    Version = "0.1.0"
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.Contents",
    Name = "Content Activities",
    Description = "Content triggers (created, published, ...) carrying the acting user, and content tasks.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows", "OrchardCore.Contents", "OrchardCore.Title"]
)]
