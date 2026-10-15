using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Grove.AspireHost;

var builder = DistributedApplication.CreateBuilder(args);

var clamAv = builder.AddClamAV("antivirus")
    .WithDataVolume("clamavdb");

builder.AddProject<Projects.Grove_Cms_Web>("PlatformCms")
    .WithExternalHttpEndpoints()
    .WithReference(clamAv)
    .WithEnvironment("Grove__Antivirus_ClamAV__Host", clamAv.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("Grove__Antivirus_ClamAV__Port", clamAv.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("Grove__Antivirus_ClamAV__ConnectTimeoutSeconds", "5")
    .WithEnvironment("Grove__Antivirus_ClamAV__TransferTimeoutSeconds", "30");

var app = builder.Build();

await app.RunAsync();
