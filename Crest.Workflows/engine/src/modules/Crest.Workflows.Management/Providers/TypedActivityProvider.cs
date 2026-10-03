using System.Runtime.CompilerServices;
using Crest.Workflows.Management.Options;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Management.Providers;

/// <summary>
/// Provides activity descriptors based on a list of activity types registered in the <see cref="ManagementOptions"/>.
/// </summary>
[UsedImplicitly]
public class TypedActivityProvider : IActivityProvider
{
    private readonly IActivityDescriber _activityDescriber;
    private readonly ManagementOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedActivityProvider"/> class.
    /// </summary>
    public TypedActivityProvider(IOptions<ManagementOptions> options, IActivityDescriber activityDescriber)
    {
        _activityDescriber = activityDescriber;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        var activityTypes = _options.ActivityTypes;
        var descriptors = await DescribeActivityTypesAsync(activityTypes, cancellationToken).ToListAsync(cancellationToken);
        return descriptors;
    }

    private async IAsyncEnumerable<ActivityDescriptor> DescribeActivityTypesAsync(IEnumerable<Type> activityTypes, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var activityType in activityTypes)
        {
            var descriptor = await _activityDescriber.DescribeActivityAsync(activityType, cancellationToken);
            yield return descriptor;
        }
    }
}