using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Id = "Crest.MemberTheme",
    Name = "Orchard Crest UI Framework Member",
    BaseTheme = "",
    Author = "Orchard Crest UI Framework",
    Website = "https://github.com/Crest/Orchard-Crest",
    Version = "4.0.0.0.0",
    Description = "A Blazor WebAssembly member portal theme for Orchard Core using Crest components.",
    // "member" is the bucket tag, the member-shell counterpart of the admin theme's
    // "admin" tag: it is how a theme declares which shell it hosts, so a fork of this
    // theme is recognized as a member theme without its id being hardcoded anywhere.
    // "crest-blazor" marks it as hosting a Crest Blazor shell document.
    Tags = ["member", "crest-blazor", "radzen"]
)]
