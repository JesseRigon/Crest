using Grove.Parties.Constants;
using Grove.Parties.PartyTypes;
using Grove.Parties.Services;
using Grove.Workflows;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Fields;

namespace Grove.Parties.Workflows;

/// <summary>
/// Raises the Parties triggers for role content items: every content type a party-type
/// provider registered, except the two base types (Person, Organization), is a role
/// composed onto a party through its <c>Party</c> picker. The registry raises; flows decide.
/// </summary>
public sealed class PartyRoleWorkflowHandler(PartyTypeCatalog catalog, IWorkflowTriggerPublisher triggers) : ContentHandlerBase
{
    public override Task CreatedAsync(CreateContentContext context) => RaiseAsync(PartiesWorkflowTriggers.RoleCreated, context.ContentItem);
    public override Task RemovedAsync(RemoveContentContext context) => RaiseAsync(PartiesWorkflowTriggers.RoleRemoved, context.ContentItem);

    private async Task RaiseAsync(string trigger, ContentItem item)
    {
        var type = catalog.Kinds.FirstOrDefault(t => t.Kind == PartyTypeKinds.Tenant && string.Equals(t.ContentType, item.ContentType, StringComparison.Ordinal));
        if (type is null || type.Key is PartiesConstants.PartyTypeKeys.Contacts or PartiesConstants.PartyTypeKeys.Organizations)
        {
            return;
        }

        var partyId = item.Get<ContentFieldSet>(item.ContentType)?.Get<OptionListField>("Party")?.SelectedIds?.FirstOrDefault();

        await triggers.PublishAsync(trigger, item.ContentItemId, new Dictionary<string, object>
        {
            ["RoleId"] = item.ContentItemId,
            ["ContentType"] = item.ContentType,
            ["TypeKey"] = type.Key,
            ["PartyId"] = partyId ?? string.Empty,
        });
    }
}
