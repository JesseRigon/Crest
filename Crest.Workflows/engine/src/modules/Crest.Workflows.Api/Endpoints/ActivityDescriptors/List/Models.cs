using Crest.Workflows.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.ActivityDescriptors.List;

[PublicAPI]
internal class Response(ICollection<ActivityDescriptor> items)
{
    public ICollection<ActivityDescriptor> Items { get; set; } = items;
    public int Count => Items.Count;
}