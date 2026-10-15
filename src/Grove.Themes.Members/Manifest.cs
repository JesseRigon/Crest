using Grove.Display.Management.Manifest;

[assembly: Theme(
    Id = "Grove.Themes.Members",
    Name = "Grove Grove UI Framework Member",
    BaseTheme = "",
    Author = "Grove Grove UI Framework",
    Website = "https://github.com/Grove/Orchard-Grove",
    Version = "4.0.0.0.0",
    Description = "A Blazor WebAssembly member portal theme for Grove using Grove components.",
    // "member" is the bucket tag, the member-shell counterpart of the admin theme's
    // "admin" tag: it is how a theme declares which shell it hosts, so a fork of this
    // theme is recognized as a member theme without its id being hardcoded anywhere.
    // "grove-blazor" marks it as hosting a Grove Blazor shell document.
    Tags = ["member", "grove-blazor", "radzen"]
)]
