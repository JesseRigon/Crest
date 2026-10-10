using Fluid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.FileStorage.AmazonS3;
using Crest.Liquid.Abstractions;

namespace Crest.Media.AmazonS3.Services;

internal sealed class AwsMediaImageCacheOptionsConfiguration : IConfigureOptions<AwsMediaImageCacheOptions>
{
    private readonly FluidParser _fluidParser;
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    public AwsMediaImageCacheOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<AwsStorageOptionsConfiguration> logger)
    {
        _fluidParser = fluidParser;
        _shellConfiguration = shellConfiguration;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public void Configure(AwsMediaImageCacheOptions options)
    {
        options.BindConfiguration(AmazonS3Constants.ConfigSections.AmazonS3ImageCache, _shellConfiguration, _logger);

        var parser = new FluidOptionsParser<AwsMediaImageCacheOptions>(_fluidParser, _shellSettings);

        try
        {
            options.BucketName = parser.ParseAndFormat(options.BucketName);
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Unable to parse Amazon S3 Media Image Cache bucket name.");
        }

        try
        {
            options.BasePath = parser.ParseAndFormat(options.BasePath);
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Unable to parse Amazon S3 Media Image Cache base path.");
        }
    }
}
