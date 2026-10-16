using System.Text.Json.Nodes;
using Grove.Contents.Management;
using ContentItem = Grove.Contents.Management.ContentItem;
using ISession = YesSql.ISession;
using ContentLifecycleRefusedExceptionMapper = Grove.Contents.Api.ContentLifecycleRefusedExceptionMapper;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Workflows.Contents.Hooks;
using Grove.Workflows.Contexts;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grove.Workflows.Tests;

// The content lifecycle (docs/content-engine.md › Behaviour is workflows): per-type hook slots
// read from the definitions, system attachments declared against a field set, refusals that
// carry field paths to the error contract's 400, and hooks and triggers carrying the item's
// previous state beside its new one.
public class ContentLifecycleHookTests
{
    private static ContentTypeDefinition Type(string name, params string[] fieldSets) =>
        new(name, name, fieldSets.Select(fieldSet => new ContentTypeFieldSetDefinition(fieldSet, new ContentFieldSetDefinition(fieldSet), [])), []);

    private static IContentDefinitionManager Definitions(params ContentTypeDefinition[] types)
    {
        var definitions = Substitute.For<IContentDefinitionManager>();
        definitions.ListTypeDefinitionsAsync().Returns(types);
        foreach (var type in types)
        {
            definitions.GetTypeDefinitionAsync(type.Name).Returns(type);
        }

        return definitions;
    }

    private sealed class FieldSetAttachments(params WorkflowFieldSetHookAttachmentDescriptor[] attachments) : IWorkflowFieldSetHookAttachmentProvider
    {
        public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments => attachments;
    }

    [Fact]
    public void A_slot_key_names_the_type_and_the_point()
    {
        Assert.Equal("content:Invoice.publishing", ContentHookSlots.Key("Invoice", ContentLifecyclePoints.Publishing));
        Assert.True(ContentHookSlots.TryParse("content:Invoice.saving-draft", out var type, out var point));
        Assert.Equal(("Invoice", ContentLifecyclePoints.SavingDraft), (type, point));
        Assert.False(ContentHookSlots.TryParse("content:Invoice.posting", out _, out _));
        Assert.False(ContentHookSlots.TryParse("order.created", out _, out _));
    }

    [Fact]
    public async Task Every_type_has_a_slot_per_point_and_workflow_definitions_have_none()
    {
        var source = new ContentHookSlotSource(Definitions(Type("Invoice"), Type("WorkflowDefinition")), [], []);

        var slots = await source.ListHookSlotsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ContentLifecyclePoints.All.Select(point => $"content:Invoice.{point}"), slots.Select(slot => slot.Key));
        Assert.Null(await source.FindHookSlotAsync("content:WorkflowDefinition.updating", TestContext.Current.CancellationToken));
        Assert.Null(await source.FindHookSlotAsync("content:Missing.updating", TestContext.Current.CancellationToken));
        Assert.NotNull(await source.FindHookSlotAsync("content:Invoice.updating", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_system_attachment_declared_against_a_field_set_reaches_every_type_that_attaches_it()
    {
        var source = new ContentHookSlotSource(
            Definitions(Type("Invoice", "Transaction", "Title"), Type("Note", "Title")),
            [new FieldSetAttachments(new WorkflowFieldSetHookAttachmentDescriptor("Transaction", ContentLifecyclePoints.Publishing, "accounting.post"))], []);

        var invoice = await source.FindHookSlotAsync("content:Invoice.publishing", TestContext.Current.CancellationToken);
        var note = await source.FindHookSlotAsync("content:Note.publishing", TestContext.Current.CancellationToken);
        var invoiceUpdating = await source.FindHookSlotAsync("content:Invoice.updating", TestContext.Current.CancellationToken);

        var attached = Assert.Single(await source.ListHookAttachmentsAsync(invoice!, TestContext.Current.CancellationToken));
        Assert.Equal(("content:Invoice.publishing", "accounting.post", true), (attached.Slot, attached.FlowKey, attached.Required));
        Assert.Empty(await source.ListHookAttachmentsAsync(note!, TestContext.Current.CancellationToken));
        Assert.Empty(await source.ListHookAttachmentsAsync(invoiceUpdating!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_failed_attachment_carries_its_field_path_or_the_item_key()
    {
        var withField = new WorkflowHookFailedException("content:Invoice.publishing", "check", "The total is negative.", [new("Transaction.Total", "The total is negative.")]);
        var withoutField = new WorkflowHookFailedException("order.created", "check", "No.");

        Assert.Equal("Transaction.Total", Assert.Single(withField.Errors).Path);
        Assert.Equal(string.Empty, Assert.Single(withoutField.Errors).Path);
    }

    private sealed class Refusing(string path, string message) : IContentLifecycleHandler
    {
        public List<ContentLifecycleContext> Hooked { get; } = [];

        public Task HookAsync(ContentLifecycleContext context)
        {
            Hooked.Add(context);
            context.Fail(path, message);
            return Task.CompletedTask;
        }

        public Task TriggerAsync(ContentLifecycleContext context) => Task.CompletedTask;

        public ValueTask<bool> NeedsPreviousAfterStoreAsync(string point, string contentType) => ValueTask.FromResult(false);
    }

    [Fact]
    public async Task A_refusing_hook_cancels_the_unit_and_throws_its_errors_by_field_path()
    {
        var handler = new Refusing("Transaction.Total", "The total is negative.");
        // A refusal cancels the document store (and so the session): nothing written commits,
        // nothing queued for after the commit runs.
        var documentStore = Substitute.For<Grove.Data.Documents.IDocumentStore>();
        var services = new ServiceCollection().AddSingleton<IContentLifecycleHandler>(handler).AddSingleton(documentStore).BuildServiceProvider();
        var lifecycle = new ContentLifecycle(services, Substitute.For<ISession>());
        var item = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice" };
        var previous = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice", DisplayText = "before" };

        var refused = await Assert.ThrowsAsync<ContentLifecycleRefusedException>(() => lifecycle.HookAsync(ContentLifecyclePoints.Updating, item, ContentPreviousState.Copy(previous)));

        Assert.Equal(ContentLifecyclePoints.Updating, refused.Point);
        Assert.Equal(new ContentLifecycleError("Transaction.Total", "The total is negative."), Assert.Single(refused.Errors));
        Assert.Same(previous, await Assert.Single(handler.Hooked).GetPreviousContentItemAsync());
        await documentStore.Received(1).CancelAsync();
    }

    private sealed class Reentering(Func<IContentLifecycle> lifecycle) : IContentLifecycleHandler
    {
        public int Calls { get; private set; }

        public async Task HookAsync(ContentLifecycleContext context)
        {
            Calls++;

            // The hook writes the item it is about: its hooks do not run again.
            await lifecycle().HookAsync(ContentLifecyclePoints.Updating, context.ContentItem, ContentPreviousState.None);
        }

        public Task TriggerAsync(ContentLifecycleContext context) => Task.CompletedTask;

        public ValueTask<bool> NeedsPreviousAfterStoreAsync(string point, string contentType) => ValueTask.FromResult(false);
    }

    [Fact]
    public async Task A_hooks_own_write_to_its_item_runs_no_hooks_again()
    {
        ContentLifecycle? lifecycle = null;
        var handler = new Reentering(() => lifecycle!);
        var services = new ServiceCollection().AddSingleton<IContentLifecycleHandler>(handler).BuildServiceProvider();
        lifecycle = new ContentLifecycle(services, Substitute.For<ISession>());

        await lifecycle.HookAsync(ContentLifecyclePoints.Updating, new ContentItem { ContentItemId = "i-1", ContentType = "Invoice" }, ContentPreviousState.None);

        Assert.Equal(1, handler.Calls);
    }

    private sealed class Observing(bool needsPrevious) : IContentLifecycleHandler
    {
        public ContentItem? TriggeredPrevious { get; private set; }

        public Task HookAsync(ContentLifecycleContext context) => Task.CompletedTask;

        public async Task TriggerAsync(ContentLifecycleContext context) => TriggeredPrevious = await context.GetPreviousContentItemAsync();

        public ValueTask<bool> NeedsPreviousAfterStoreAsync(string point, string contentType) => ValueTask.FromResult(needsPrevious);
    }

    [Fact]
    public async Task The_previous_state_is_captured_before_the_store_only_for_a_trigger_that_needs_it()
    {
        var item = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice", DisplayText = "after" };
        var previous = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice", DisplayText = "before" };

        var needing = new Observing(needsPrevious: true);
        var lifecycle = new ContentLifecycle(new ServiceCollection().AddSingleton<IContentLifecycleHandler>(needing).BuildServiceProvider(), Substitute.For<ISession>());
        await lifecycle.HookAsync(ContentLifecyclePoints.Updating, item, ContentPreviousState.Copy(previous));
        await lifecycle.TriggerAsync(ContentLifecyclePoints.Updating, item, ContentPreviousState.Copy(previous));
        Assert.Same(previous, needing.TriggeredPrevious);

        // A stored version nobody asked for before the store is not read: afterwards it is the new state.
        var notNeeding = new Observing(needsPrevious: false);
        var session = Substitute.For<ISession>();
        lifecycle = new ContentLifecycle(new ServiceCollection().AddSingleton<IContentLifecycleHandler>(notNeeding).BuildServiceProvider(), session);
        var stored = new ContentItem { Id = 42, ContentItemId = "i-2", ContentType = "Invoice" };
        await lifecycle.HookAsync(ContentLifecyclePoints.Updating, stored, ContentPreviousState.Of(stored));
        await lifecycle.TriggerAsync(ContentLifecyclePoints.Updating, stored, ContentPreviousState.Of(stored));
        Assert.Null(notNeeding.TriggeredPrevious);
        await session.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void A_refusal_answers_400_with_its_errors_under_their_field_paths()
    {
        var refused = new ContentLifecycleRefusedException(ContentLifecyclePoints.Publishing, [new("Transaction.Total", "The total is negative."), new("", "Not now.")]);

        var result = new ContentLifecycleRefusedExceptionMapper().Map(refused, new DefaultHttpContext());

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(["The total is negative."], problem.ProblemDetails.Errors["Transaction.Total"]);
        Assert.Equal(["Not now."], problem.ProblemDetails.Errors[""]);
    }

    [Fact]
    public async Task A_trigger_carries_the_items_new_and_previous_state()
    {
        var sender = Substitute.For<IStimulusSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<StimulusMetadata?>(), Arg.Any<CancellationToken>())
            .Returns(new SendStimulusResult([]));
        var user = Substitute.For<IWorkflowUserContextAccessor>();
        user.CaptureSealed().Returns("actor");
        var queue = new Units.WorkflowStimulusQueue(sender, new Units.WorkflowAfterCommit(new Units.WorkflowUnitOfWork(), NullLogger<Units.WorkflowAfterCommit>.Instance), NullLogger<Units.WorkflowStimulusQueue>.Instance);
        var services = new ServiceCollection().AddSingleton(queue).AddSingleton(user).BuildServiceProvider();
        var handler = new WorkflowContentLifecycleHandler(services);
        var item = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice", DisplayText = "after" };
        var previous = new ContentItem { ContentItemId = "i-1", ContentType = "Invoice", DisplayText = "before" };

        await handler.TriggerAsync(new ContentLifecycleContext(ContentLifecyclePoints.Updating, item, previous));

        await sender.Received(1).SendAsync(
            "Grove.Content.ContentUpdated",
            Arg.Is<object>(stimulus => stimulus is Contents.Stimuli.ContentEventStimulus && ((Contents.Stimuli.ContentEventStimulus)stimulus).ContentType == "Invoice"),
            Arg.Is<StimulusMetadata?>(m => m!.CorrelationId == "i-1"
                && ReferenceEquals(m.Input![ContentLifecycleInputKeys.ContentItem], item)
                && ReferenceEquals(m.Input[ContentLifecycleInputKeys.PreviousContentItem], previous)
                && Equals(m.Input[ContentLifecycleInputKeys.Point], ContentLifecyclePoints.Updating)),
            Arg.Any<CancellationToken>());
    }
}
