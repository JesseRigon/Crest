using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Crest.AspireHost;

var builder = DistributedApplication.CreateBuilder(args);

var clamAv = builder.AddClamAV("antivirus")
    .WithDataVolume("clamavdb");

builder.AddProject<Projects.Crest_Cms_Web>("PlatformCms")
    .WithExternalHttpEndpoints()
    .WithReference(clamAv)
    .WithEnvironment("Crest__Antivirus_ClamAV__Host", clamAv.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("Crest__Antivirus_ClamAV__Port", clamAv.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("Crest__Antivirus_ClamAV__ConnectTimeoutSeconds", "5")
    .WithEnvironment("Crest__Antivirus_ClamAV__TransferTimeoutSeconds", "30");

var app = builder.Build();

await app.RunAsync();
