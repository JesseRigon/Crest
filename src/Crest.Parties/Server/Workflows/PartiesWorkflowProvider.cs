using Crest.Workflows;

namespace Crest.Parties.Workflows;

/// <summary>The triggers the Parties registry raises.</summary>
public static class PartiesWorkflowTriggers
{
    public const string RoleCreated = "party.role-created";
    public const string RoleRemoved = "party.role-removed";
}

/// <summary>
/// What Parties registers with the workflow registry: a role (Customer, Vendor, Employee,
/// Lead, ...) was composed onto a party, or removed from it. Payload: RoleId, ContentType,
/// TypeKey (the party type's registry key) and PartyId (the Person or Organization).
/// </summary>
public sealed class PartiesWorkflowProvider : IWorkflowTriggerProvider, IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("parties.create-role", "Create party role", WorkflowsConstants.Objects.Party, "Crest.Parties.CreatePartyRole", "Gives a party a role of a registered party type; keeps an existing one.", 10),
        new("parties.resolve-party", "Resolve party", WorkflowsConstants.Objects.Party, "Crest.Parties.ResolveParty", "Tells a role from its base Person or Organization, for flows that read party fields.", 20),
    ];

    public IEnumerable<WorkflowTriggerDescriptor> Triggers =>
    [
        new(PartiesWorkflowTriggers.RoleCreated, "Party role created", WorkflowsConstants.Objects.Party, "A role content item was created on a party: payload RoleId, ContentType, TypeKey, PartyId.", 10),
        new(PartiesWorkflowTriggers.RoleRemoved, "Party role removed", WorkflowsConstants.Objects.Party, "A role content item was removed: payload RoleId, ContentType, TypeKey, PartyId.", 20),
    ];
}
