using Grove.ContentManagement.Handlers;
using Grove.Environment.Shell.Scope;
using Grove.Media.Files;
using Grove.Parties.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Members.Handlers;

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

/// <summary>
/// A new organization's default permission system (docs/permissions.md › Layers): the
/// tenant's allow-listed groups, cloned with their roles when the organization is published,
/// then the organization's own. Idempotent by template, so a republish clones nothing new.
/// The tenant's own organization holds staff, whose groups are the tenant's: it gets none.
/// </summary>
public sealed class OrganizationAccessGroupsHandler : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context)
    {
        if (context.ContentItem.ContentType != PartiesConstants.ContentTypes.Organization)
        {
            return Task.CompletedTask;
        }

        var id = context.ContentItem.ContentItemId;
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            if (services.GetService<Grove.Parties.Services.ITenantOrganizationService>() is { } tenant && await tenant.IsTenantOrganizationAsync(id))
            {
                return;
            }

            await services.GetRequiredService<Grove.Access.IAccessGroupService>().CloneDefaultsAsync(id);
        });
        return Task.CompletedTask;
    }

    public override Task RemovedAsync(RemoveContentContext context)
    {
        if (context.ContentItem.ContentType != PartiesConstants.ContentTypes.Organization || !context.NoActiveVersionLeft)
        {
            return Task.CompletedTask;
        }

        var id = context.ContentItem.ContentItemId;
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<Grove.Access.IAccessGroupService>().RemoveOrganizationAsync(id));
        return Task.CompletedTask;
    }
}
