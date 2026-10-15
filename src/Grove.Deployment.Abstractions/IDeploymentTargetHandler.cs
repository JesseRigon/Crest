using Microsoft.Extensions.FileProviders;

namespace Grove.Deployment;

public interface IDeploymentTargetHandler
{
    Task ImportFromFileAsync(IFileProvider fileProvider);
}
