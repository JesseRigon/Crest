using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Parties",
    Author = "Grove",
    Website = "https://grove.local",
    Version = "1.0.0",
    Description = "The platform-wide party model: Person and Organization profiles that domain modules attach roles to.",
    Category = "Business"
)]

[assembly: Feature(
    Id = "Grove.Parties",
    Name = "Parties",
    Description = "Person and Organization party content types shared by every domain module, with bag-contained contact points and addresses.",
    Dependencies = ["Grove.Access", 
        "Grove.Workflows",
        "Grove.Contents",
        "Grove.Contents.Fields",
        "Grove.Contents.Fields.Indexing.SQL.UserPicker",
        "Grove.Contents.Title",
        "Grove.Contents.BagParts",
        "Grove.Contents.PartLists",
        "Grove.Contents.OptionPicker",
        "Grove.Data.Regions"
    ],
    Category = "Business",
    // Above Contents: the parties mapper answers for party items before the content mapper,
    // whose candidates it keeps (docs/access.md, mapper roles and priority).
    Priority = "1"
)]

[assembly: Feature(
    Id = "Grove.Parties.SampleRole",
    Name = "Parties: sample role",
    Description = "A sample party role, so the generic role pages can be exercised without a host's modules.",
    Dependencies = ["Grove.Parties"],
    Category = "Samples"
)]
