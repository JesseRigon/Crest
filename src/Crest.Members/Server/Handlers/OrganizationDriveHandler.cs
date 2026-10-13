using Crest.ContentManagement.Handlers;
using Crest.Environment.Shell.Scope;
using Crest.Media.Files;
using Crest.Parties.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Members.Handlers;

/// <summary>
/// An organization drive with each organization (docs/media.md § 3, requirement 12), while the
/// tenant uses organization drives: on publishing an organization, after the request's unit of
/// work so the drive's writes stay out of the content item's session.
/// </summary>
public sealed class OrganizationDriveHandler : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context)
    {
        var item = context.ContentItem;
        if (item.ContentType != PartiesConstants.ContentTypes.Organization)
        {
            return Task.CompletedTask;
        }

        var id = item.ContentItemId;
        var name = item.DisplayText ?? id;
        ShellScope.AddDeferredTask(scope =>
            scope.ServiceProvider.GetService<IMediaDriveProvisioning>() is { } provisioning
                ? provisioning.EnsureOrganizationDriveAsync(id, name)
                : Task.CompletedTask);
        return Task.CompletedTask;
    }
}
