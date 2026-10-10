using Crest.Workflows.Studio.Contracts;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Components.Forms;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

[UsedImplicitly]
/// <summary>
/// Represents the notification published when an importing is file.
/// </summary>
public record ImportingFile(IBrowserFile File) : INotification;