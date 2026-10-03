using Crest.Workflows.Studio.Contracts;
using JetBrains.Annotations;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

[UsedImplicitly]
/// <summary>
/// Represents the notification published when an imported is json.
/// </summary>
public record ImportedJson(string Json) : INotification;