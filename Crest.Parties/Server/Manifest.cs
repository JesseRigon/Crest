using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Parties",
    Author = "OrchardCore.Crest",
    Website = "https://crest.local",
    Version = "1.0.0",
    Description = "The platform-wide party model: Person and Organization profiles that domain modules attach roles to.",
    Category = "Business"
)]

[assembly: Feature(
    Id = "Crest.Parties",
    Name = "Parties",
    Description = "Person and Organization party content types shared by every domain module, with bag-contained contact points and addresses.",
    Dependencies = [
        "Crest.Workflows",
        "OrchardCore.Contents",
        "OrchardCore.ContentFields",
        "OrchardCore.ContentFields.Indexing.SQL.UserPicker",
        "OrchardCore.Title",
        "OrchardCore.Flows",
        "Crest.ContentPartLists",
        "Crest.Regions"
    ],
    Category = "Business"
)]
