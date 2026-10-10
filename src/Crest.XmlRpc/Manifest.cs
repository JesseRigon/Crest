using Crest.Modules.Manifest;

[assembly: Module(
    Name = "XML-RPC",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.XmlRpc",
    Name = "XML-RPC",
    Description = "The XML-RPC module enables creation of contents from client applications such as Open Live Writer.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = "Crest.RemotePublishing",
    Name = "Remote Publishing",
    Description = "The remote publishing feature enables creation of contents from client applications such as Open Live Writer.",
    Dependencies = ["Crest.XmlRpc"],
    Category = "Infrastructure"
)]
