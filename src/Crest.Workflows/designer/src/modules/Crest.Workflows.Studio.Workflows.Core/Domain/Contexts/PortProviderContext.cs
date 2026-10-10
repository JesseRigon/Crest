using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Contexts;

/// <summary>
/// Represents the context for a port provider.
/// </summary>
/// <param name="ActivityDescriptor">The descriptor of te activity for which ports are being provided.</param> 
/// <param name="Activity">The activity for which ports are being provided.</param>
public record PortProviderContext(ActivityDescriptor ActivityDescriptor, JsonObject Activity);