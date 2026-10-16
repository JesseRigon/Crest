using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Handlers;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using ContentValidationErrors = Grove.Contents.Api.ContentValidationErrors;
using ContentItem = Grove.Contents.Management.ContentItem;
using Grove.Infrastructure.Html;
using Grove.Contents.SiteMenu.Services;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

/// <summary>
/// The rules the retired MVC editor drivers enforced, now on every save path (the content
/// handlers' ValidatingAsync), and the content items API's error map keyed by the editor tree's
/// paths.
/// </summary>
public sealed class ContentValidationTests
{
    private static readonly IStringLocalizer<TextFieldHandler> s_text = Localizer<TextFieldHandler>();

    [Fact]
    public async Task A_text_field_shorter_than_its_minimum_fails_under_its_field_path()
    {
        var (item, definition) = Item("TextField", new JsonObject { ["TextFieldSettings"] = new JsonObject { ["MinLength"] = 5 } });
        var context = FieldContext(item, definition);
        var handler = new TextFieldHandler(null!, null!, s_text);

        await handler.ValidatingAsync(context, new TextField { Text = "abc" });

        var error = Assert.Single(context.ContentValidateResult.Errors);
        Assert.Equal("Article.Subtitle.Text", Assert.Single(error.MemberNames));
    }

    [Fact]
    public async Task A_text_field_longer_than_its_maximum_fails()
    {
        var (item, definition) = Item("TextField", new JsonObject { ["TextFieldSettings"] = new JsonObject { ["MaxLength"] = 2 } });
        var context = FieldContext(item, definition);

        await new TextFieldHandler(null!, null!, s_text).ValidatingAsync(context, new TextField { Text = "abc" });

        Assert.False(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task A_text_field_within_its_bounds_passes()
    {
        var (item, definition) = Item("TextField", new JsonObject { ["TextFieldSettings"] = new JsonObject { ["MinLength"] = 1, ["MaxLength"] = 5 } });
        var context = FieldContext(item, definition);

        await new TextFieldHandler(null!, null!, s_text).ValidatingAsync(context, new TextField { Text = "abc" });

        Assert.True(context.ContentValidateResult.Succeeded);
    }

    [Theory]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/about", true)]
    [InlineData("https://example.com/a?b=c", true)]
    public async Task A_menu_link_holds_a_safe_well_formed_url(string url, bool valid)
    {
        var sanitizer = Substitute.For<IHtmlSanitizerService>();
        sanitizer.Sanitize(Arg.Any<string>()).Returns(call => call.Arg<string>()!.Contains("javascript:", StringComparison.Ordinal) ? "<a></a>" : call.Arg<string>());
        var behaviour = new MenuBehaviour(null!, sanitizer, HtmlEncoder.Default, Localizer<MenuBehaviour>());

        Assert.Equal(valid, behaviour.IsValidUrl(url));
    }

    [Fact]
    public void Errors_are_keyed_as_the_editor_tree_keys_its_editors()
    {
        var (_, field) = Item("TextField", []);
        var type = new ContentTypeDefinition("Article", "Article",
            [field.ContentTypeFieldSetDefinition, new ContentTypeFieldSetDefinition("Title", new ContentFieldSetDefinition("Title"), [])], []);

        var map = ContentValidationErrors.ToMap(
            [
                new ValidationResult("Too short.", ["Article.Subtitle.Text"]),
                new ValidationResult("Required.", ["Title.Title"]),
                new ValidationResult("Broken."),
            ],
            type);

        Assert.Equal(["Too short."], map["Article.Subtitle"]);
        Assert.Equal(["Required."], map["Title"]);
        Assert.Equal(["Broken."], map[string.Empty]);
    }

    private static (ContentItem Item, ContentFieldDefinition Field) Item(string fieldType, JsonObject settings)
    {
        var field = new ContentFieldDefinition(new ContentFieldTypeDefinition(fieldType), "Subtitle", settings);
        var part = new ContentFieldSetDefinition("Article", [field], []);
        var typeFieldSet = new ContentTypeFieldSetDefinition("Article", part, []);
        field.ContentTypeFieldSetDefinition = typeFieldSet;
        return (new ContentItem { ContentType = "Article" }, field);
    }

    private static ValidateContentFieldContext FieldContext(ContentItem item, ContentFieldDefinition definition) =>
        new(item) { ContentFieldDefinition = definition, FieldSetName = "Article" };

    private static IStringLocalizer<T> Localizer<T>()
    {
        var localizer = Substitute.For<IStringLocalizer<T>>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>()!, call.Arg<string>()!));
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call => new LocalizedString(call.Arg<string>()!, string.Format(call.Arg<string>()!, call.Arg<object[]>()!)));
        return localizer;
    }
}
