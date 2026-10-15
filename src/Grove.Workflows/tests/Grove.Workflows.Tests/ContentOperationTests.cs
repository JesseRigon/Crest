using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;
using Grove.Workflows.Contents;
using Grove.Workflows.Models;
using Grove.Workflows.Security;
using Xunit;

namespace Grove.Workflows.Tests;

// The registry's content operations (docs/workflows.md › Tasks › 3): one engine descriptor
// per content type and operation, typed from the type's fields, carrying the operation's
// permission asked against the type.
public class ContentOperationTests
{
    private static ContentTypeDefinition Invoice() => new(
        "Invoice",
        "Invoice",
        [
            new ContentTypePartDefinition("Invoice", new ContentPartDefinition("Invoice",
            [
                new ContentPartFieldDefinition(new ContentFieldDefinition("TextField"), "Number", new JsonObject()),
                new ContentPartFieldDefinition(new ContentFieldDefinition("NumericField"), "Amount", new JsonObject()),
                new ContentPartFieldDefinition(new ContentFieldDefinition("OptionPickerField"), "Customer", new JsonObject()),
                new ContentPartFieldDefinition(new ContentFieldDefinition("MediaField"), "Scan", new JsonObject()),
            ], new JsonObject()), new JsonObject()),
        ],
        new JsonObject());

    private static ActivityDescriptor Template() => new()
    {
        Inputs =
        [
            new InputDescriptor { Name = nameof(ContentOperation.ContentItemId), Type = typeof(string) },
            new InputDescriptor { Name = nameof(ContentOperation.DisplayText), Type = typeof(string) },
            new InputDescriptor { Name = nameof(ContentOperation.Publish), Type = typeof(bool) },
        ],
        Outputs = [new OutputDescriptor { Name = nameof(ContentOperation.ItemId), Type = typeof(string) }],
    };

    [Fact]
    public void Create_takes_the_types_fields_as_typed_inputs_and_no_item_id()
    {
        var descriptor = ContentOperationActivityProvider.Describe(Invoice(), ContentOperationKind.Create, Template());

        Assert.Equal(typeof(ContentOperation), descriptor.ClrType);
        Assert.Equal("Grove.Content.CreateInvoice", descriptor.TypeName);
        Assert.Equal("Invoice", descriptor.CustomProperties[WorkflowsConstants.ContentTypeDescriptorProperty]);
        Assert.Equal("Create", descriptor.CustomProperties[WorkflowsConstants.ContentOperationDescriptorProperty]);
        Assert.DoesNotContain(descriptor.Inputs, input => input.Name == nameof(ContentOperation.ContentItemId));
        Assert.Contains(descriptor.Inputs, input => input.Name == nameof(ContentOperation.Publish));

        // A field kind with no typed input (media) is left to Copy fields.
        var fields = descriptor.Inputs.Where(input => input.IsSynthetic).ToList();
        Assert.Equal([typeof(string), typeof(decimal?), typeof(IList<string>)], fields.Select(input => input.Type));
        Assert.True(ContentOperationActivityProvider.TryFieldOf(descriptor, fields[1], out var path, out var fieldType));
        Assert.Equal("Invoice.Amount", path);
        Assert.Equal("NumericField", fieldType);
    }

    [Theory]
    [InlineData(ContentOperationKind.Create, "EditOwnContent")]
    [InlineData(ContentOperationKind.Update, "EditOwnContent")]
    [InlineData(ContentOperationKind.Delete, "DeleteOwnContent")]
    [InlineData(ContentOperationKind.Publish, "PublishOwnContent")]
    [InlineData(ContentOperationKind.Unpublish, "PublishOwnContent")]
    public void The_gate_asks_the_owner_variation_against_the_type(ContentOperationKind operation, string permission)
    {
        var descriptor = ContentOperationActivityProvider.Describe(Invoice(), operation, Template());

        Assert.Equal(permission, ActivityAccessGateMiddleware.RequiredPermission(descriptor));
        Assert.Equal("Invoice", ActivityAccessGateMiddleware.PermissionResource(descriptor));
    }

    [Theory]
    [InlineData(ContentOperationKind.Delete)]
    [InlineData(ContentOperationKind.Publish)]
    [InlineData(ContentOperationKind.Unpublish)]
    public void Operations_that_write_no_fields_take_the_item_id_only(ContentOperationKind operation)
    {
        var descriptor = ContentOperationActivityProvider.Describe(Invoice(), operation, Template());

        Assert.Equal([nameof(ContentOperation.ContentItemId)], descriptor.Inputs.Select(input => input.Name));
    }

    [Fact]
    public void The_node_is_built_for_its_type_and_operation()
    {
        var descriptor = ContentOperationActivityProvider.Describe(Invoice(), ContentOperationKind.Update, Template());

        var activity = (ContentOperation)descriptor.Constructor(new ActivityConstructorContext(descriptor, type => new ActivityConstructionResult((IActivity)Activator.CreateInstance(type)!))).Activity;

        Assert.Equal("Invoice", activity.ContentType);
        Assert.Equal("Update", activity.Operation);
        Assert.Equal(descriptor.TypeName, activity.Type);
    }

    [Fact]
    public void Values_become_the_fields_json()
    {
        Assert.Equal("INV-1", ContentFieldValues.ToJson("TextField", "INV-1")!["Text"]!.GetValue<string>());
        Assert.Equal(12.5m, ContentFieldValues.ToJson("NumericField", 12.5)!["Value"]!.GetValue<decimal>());
        Assert.Equal(["a", "b"], ((JsonArray)ContentFieldValues.ToJson("OptionPickerField", "a, b")!["SelectedIds"]!).Select(node => node!.GetValue<string>()));
        Assert.Null(ContentFieldValues.ToJson("MediaField", "x"));
    }
}
