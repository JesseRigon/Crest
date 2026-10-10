using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Workflows;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Handlers;

namespace Crest.Parties.Workflows;

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
        var type = catalog.Types.FirstOrDefault(t => t.Kind == PartyTypeKinds.Tenant && string.Equals(t.ContentType, item.ContentType, StringComparison.Ordinal));
        if (type is null || type.Key is PartiesConstants.PartyTypeKeys.Contacts or PartiesConstants.PartyTypeKeys.Organizations)
        {
            return;
        }

        var partyId = item.Get<ContentPart>(item.ContentType)?.Get<ContentPickerField>("Party")?.ContentItemIds?.FirstOrDefault();

        await triggers.PublishAsync(trigger, item.ContentItemId, new Dictionary<string, object>
        {
            ["RoleId"] = item.ContentItemId,
            ["ContentType"] = item.ContentType,
            ["TypeKey"] = type.Key,
            ["PartyId"] = partyId ?? string.Empty,
        });
    }
}
