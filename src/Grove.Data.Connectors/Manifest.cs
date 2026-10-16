using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Connectors",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The connector engine: connections to external services, their credentials and how every call to them runs.",
    Category = "Data"
)]

[assembly: Feature(
    Id = "Grove.Data.Connectors",
    Name = "Connectors",
    Description = "Connections to external services (REST, SOAP, GraphQL, SQL, webhooks), sealed tenant and user credentials, OAuth, and the guarded execution pipeline. Adds the Connections screen and each staff user's Connected accounts page.",
    Dependencies = ["Grove.Access"],
    Category = "Data"
)]
