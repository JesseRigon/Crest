using Grove.Data.Storage;
using System.Text.Json.Nodes;
using Grove.Deployment;

namespace Grove.Media.Deployment;

public sealed class MediaDeploymentSource
    : DeploymentSourceBase<MediaDeploymentStep>
{
    private readonly IMediaFileStore _mediaFileStore;

    public MediaDeploymentSource(IMediaFileStore mediaFileStore)
    {
        _mediaFileStore = mediaFileStore;
    }

    protected override async Task ProcessAsync(MediaDeploymentStep step, DeploymentPlanResult result)
    {
        IAsyncEnumerable<string> paths = null;

        if (step.IncludeAll)
        {
            paths = _mediaFileStore.GetDirectoryContentAsync(null, true).Where(e => !e.IsDirectory).Select(e => e.Path);
        }
        else
        {
            paths = new List<string>(step.FilePaths ?? []).ToAsyncEnumerable();

            foreach (var directoryPath in step.DirectoryPaths ?? [])
            {
                paths = paths.Concat(_mediaFileStore.GetDirectoryContentAsync(directoryPath, true).Where(e => !e.IsDirectory).Select(e => e.Path));
            }

            paths = paths.OrderBy(p => p);
        }

        var candidates = await paths.Select(p => new MediaDeploymentStepModel { SourcePath = p, TargetPath = p }).ToArrayAsync();
        var output = new List<MediaDeploymentStepModel>(candidates.Length);

        foreach (var path in candidates)
        {
            // A file of the media library still in quarantine, or rejected, has no bytes to export.
            Stream stream;
            try
            {
                stream = await _mediaFileStore.GetFileStreamAsync(path.SourcePath);
            }
            catch (FileStoreException)
            {
                continue;
            }

            using (stream)
            {
                await result.FileBuilder.SetFileAsync(path.SourcePath, stream);
            }

            output.Add(path);
        }

        // Adding media files
        result.Steps.Add(new JsonObject
        {
            ["name"] = "media",
            ["Files"] = JArray.FromObject(output),
        });
    }

    private sealed class MediaDeploymentStepModel
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
    }
}
