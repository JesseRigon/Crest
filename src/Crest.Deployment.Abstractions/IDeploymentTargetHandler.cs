using Microsoft.Extensions.FileProviders;

namespace Crest.Deployment;

public interface IDeploymentTargetHandler
{
    Task ImportFromFileAsync(IFileProvider fileProvider);
}
