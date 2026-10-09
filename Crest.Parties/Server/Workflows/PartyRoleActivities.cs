using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using YesSql;

namespace Crest.Parties.Workflows;

/// <summary>
/// Composes a role (Customer, Prospect, Vendor, ...) onto a party through the Parties
/// registry: the party type is a registry key (<c>customers</c>, <c>prospects</c>), the
/// party defaults to the trigger payload's <c>PartyId</c>. A party that already holds the
/// role keeps it: the activity ends on Done with that role's id and <c>Created</c> false,
/// so a flow can run twice without doubling roles. The new role raises
/// <c>party.role-created</c> like any other.
/// </summary>
[Activity("Crest.Parties", "Parties", "Gives a party a role of a registered party type (e.g. make a prospect's party a customer).", DisplayName = "Create party role")]
[FlowNode("Done", "Failed")]
public class CreatePartyRole : Activity
{
    [Input(DisplayName = "Party type", Description = "The registered party type key of the role, e.g. customers or prospects.", UIHint = InputUIHints.SingleLine)]
    public Input<string> TypeKey { get; set; } = null!;

    [Input(DisplayName = "Party id", Description = "The Person or Organization. Empty = the PartyId of the trigger payload.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> PartyId { get; set; } = null!;

    [Output(Description = "The role's content item id (new or existing).")]
    public Output<string?> RoleId { get; set; } = null!;

    [Output(Description = "True when the role was created now, false when the party already had it.")]
    public Output<bool> Created { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var catalog = context.GetRequiredService<PartyTypeCatalog>();
        var typeKey = TypeKey.GetOrDefault(context)?.Trim();
        var type = string.IsNullOrEmpty(typeKey) ? null : catalog.Find(typeKey);
        if (type is null || type.Kind != PartyTypeKinds.Tenant || type.Key is PartiesConstants.PartyTypeKeys.Contacts or PartiesConstants.PartyTypeKeys.Organizations)
        {
            await FailAsync(context, $"'{typeKey}' is not a registered role party type.");
            return;
        }

        var partyId = PartyId.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(partyId))
        {
            var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload);
            partyId = payload is not null && payload.TryGetValue("PartyId", out var id) ? id?.ToString() : null;
        }

        var contentManager = context.GetRequiredService<IContentManager>();
        var party = string.IsNullOrWhiteSpace(partyId) ? null : await contentManager.GetAsync(partyId, VersionOptions.Latest);
        if (party is null || party.ContentType is not (PartiesConstants.ContentTypes.Person or PartiesConstants.ContentTypes.Organization))
        {
            await FailAsync(context, $"'{partyId}' is not a Person or Organization.");
            return;
        }

        var existing = await FindRoleAsync(context.GetRequiredService<ISession>(), type.ContentType, party.ContentItemId);
        if (existing is not null)
        {
            RoleId.Set(context, existing.ContentItemId);
            Created.Set(context, false);
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        var role = await contentManager.NewAsync(type.ContentType);
        role.DisplayText = party.DisplayText;
        role.Alter<ContentPart>(type.ContentType, part => part.Alter<ContentPickerField>("Party", field => field.ContentItemIds = [party.ContentItemId]));
        await contentManager.CreateAsync(role, VersionOptions.Published);

        RoleId.Set(context, role.ContentItemId);
        Created.Set(context, true);
        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    // Roles of one type are few per tenant compared to parties; the picker lives in the item
    // JSON, so the match is made in memory over the type's latest items.
    private static async Task<ContentItem?> FindRoleAsync(ISession session, string contentType, string partyId)
    {
        var roles = await session.Query<ContentItem, ContentItemIndex>(i => i.ContentType == contentType && i.Latest).ListAsync();
        return roles.FirstOrDefault(r => r.Get<ContentPart>(contentType)?.Get<ContentPickerField>("Party")?.ContentItemIds?.Contains(partyId) == true);
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILogger<CreatePartyRole>>().LogWarning("Create party role failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>
/// Resolves a party reference for the flows that copy or read party fields: given a role
/// (Customer, Vendor, ...) or a base party (Person, Organization), tells which is which. A
/// role's fields live on the role item, the person's or organization's on the base party;
/// a Copy fields step reads from whichever holds the field, and nothing walks the graph
/// implicitly. The id defaults to the trigger payload's <c>PartyId</c>.
/// </summary>
[Activity("Crest.Parties", "Parties", "Resolves a party id into its role, its base Person or Organization and their types.", DisplayName = "Resolve party")]
[FlowNode("Done", "Failed")]
public class ResolveParty : Activity
{
    [Input(DisplayName = "Party id", Description = "A role (Customer, Vendor, ...) or a Person / Organization id. Empty = the PartyId of the trigger payload.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> PartyId { get; set; } = null!;

    [Output(Description = "The role's content item id, when the id was (or resolved to) a role; null for a base party.")]
    public Output<string?> RoleId { get; set; } = null!;

    [Output(Description = "The role's content type (Customer, Vendor, ...), when there is one.")]
    public Output<string?> RoleContentType { get; set; } = null!;

    [Output(Description = "The role's registered party type key (customers, vendors, ...), when there is one.")]
    public Output<string?> RoleTypeKey { get; set; } = null!;

    [Output(Description = "The Person or Organization behind the reference.")]
    public Output<string?> BasePartyId { get; set; } = null!;

    [Output(Description = "Person or Organization.")]
    public Output<string?> BasePartyContentType { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var partyId = PartyId.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(partyId))
        {
            var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload);
            partyId = payload is not null && payload.TryGetValue("PartyId", out var id) ? id?.ToString() : null;
        }

        var contentManager = context.GetRequiredService<IContentManager>();
        var item = string.IsNullOrWhiteSpace(partyId) ? null : await contentManager.GetAsync(partyId, VersionOptions.Latest);
        if (item is null)
        {
            await FailAsync(context, $"Party '{partyId}' was not found.");
            return;
        }

        if (item.ContentType is PartiesConstants.ContentTypes.Person or PartiesConstants.ContentTypes.Organization)
        {
            BasePartyId.Set(context, item.ContentItemId);
            BasePartyContentType.Set(context, item.ContentType);
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        var type = context.GetRequiredService<PartyTypeCatalog>().Types.FirstOrDefault(t => string.Equals(t.ContentType, item.ContentType, StringComparison.Ordinal));
        var baseId = item.Get<ContentPart>(item.ContentType)?.Get<ContentPickerField>("Party")?.ContentItemIds?.FirstOrDefault();
        var baseParty = baseId is null ? null : await contentManager.GetAsync(baseId, VersionOptions.Latest);
        if (type is null || baseParty is null)
        {
            await FailAsync(context, type is null ? $"'{item.ContentType}' is not a registered party role type." : $"Role '{item.ContentItemId}' points at no Person or Organization.");
            return;
        }

        RoleId.Set(context, item.ContentItemId);
        RoleContentType.Set(context, item.ContentType);
        RoleTypeKey.Set(context, type.Key);
        BasePartyId.Set(context, baseParty.ContentItemId);
        BasePartyContentType.Set(context, baseParty.ContentType);
        context.JournalData["BasePartyId"] = baseParty.ContentItemId;
        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.JournalData["Error"] = reason;
        context.GetRequiredService<ILogger<ResolveParty>>().LogWarning("Resolve party failed: {Reason}", reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}
