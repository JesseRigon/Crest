using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.SiteMenu.Models;
using Grove.Contents.SiteMenu.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.SiteMenu.Workflows;

/// <summary>Sanitizes the markup of the menu's HTML items whose type's settings say so. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.SiteMenu", "Content", "Sanitizes the markup of the menu's HTML items whose type's settings say so.", DisplayName = "Sanitize menu item markup")]
[FlowNode("Done", "Refused")]
public class SanitizeMenuItemMarkup : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<MenuBehaviour>().SanitizeAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses a menu holding an item whose address is not a well-formed, safe URL. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.SiteMenu", "Content", "Refuses a menu holding an item whose address is not a well-formed, safe URL.", DisplayName = "Check menu item addresses")]
[FlowNode("Done", "Refused")]
public class CheckMenuItemAddresses : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var refusal = await context.GetRequiredService<MenuBehaviour>().AddressRefusalAsync(context.GetWorkflowInput<ContentItem>());
        if (refusal is not { } refused)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, refused.Error);
        Field.Set(context, refused.Field);
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>
/// The menu items' behaviour as system flows (docs/content-engine.md › Behaviour is workflows), attached to the Menu field set because embedded items have no lifecycle of their own: the HTML items' markup sanitized on creating, updating, saving a draft and importing; every item's address checked on validating.
/// </summary>
public sealed class MenuWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string SanitizeFlowKey = "menu.sanitize";
    public const string CheckAddressesFlowKey = "menu.check-addresses";

    private static readonly string FieldSet = ContentFieldSetName.Of<MenuFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("menu.sanitize", "Sanitize menu item markup", "content", "Grove.Contents.SiteMenu.SanitizeMenuItemMarkup", "Sanitizes the markup of the menu's HTML items whose type's settings say so.", 10, Category: "Menu"),
        new("menu.check-addresses", "Check menu item addresses", "content", "Grove.Contents.SiteMenu.CheckMenuItemAddresses", "Refuses a menu holding an item whose address is not a well-formed, safe URL.", 20, Category: "Menu"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(SanitizeFlowKey, "Menu: sanitize item markup", "content", ShippedFlows.Read(typeof(MenuWorkflowProvider).Assembly, "menu-sanitize.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(CheckAddressesFlowKey, "Menu: check item addresses", "content", ShippedFlows.Read(typeof(MenuWorkflowProvider).Assembly, "menu-check-addresses.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Updating, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.SavingDraft, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Importing, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Validating, CheckAddressesFlowKey, 10),
    ];
}
