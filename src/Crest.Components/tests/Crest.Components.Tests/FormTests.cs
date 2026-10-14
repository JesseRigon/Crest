using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Components.Content;
using Crest.Components.Forms;
using Crest.DisplayManagement.Components;
using Xunit;

namespace Crest.Components.Tests;

// The form and content primitives that replace the widgets (docs/widgets.md).
public sealed class FormTests
{
    [Fact]
    public void TheFormSubmitsEachInputByNameAndChecksRequiredOnes()
    {
        var form = new FormContext();
        JsonNode? email = null;
        form.Register(new FormField("email", "Email", Required: true, () => email));
        form.Register(new FormField("subscribe", null, Required: false, () => JsonValue.Create(true)));

        Assert.Equal(["email"], form.CheckRequired().Keys);

        email = JsonValue.Create("a@example.com");

        Assert.Empty(form.CheckRequired());
        var values = form.Values();
        Assert.Equal("a@example.com", values["email"]!.GetValue<string>());
        Assert.True(values["subscribe"]!.GetValue<bool>());
    }

    [Fact]
    public void ErrorsAreReadByInputNameAndTheFormsOwnComeFirst()
    {
        var form = new FormContext();
        var changes = 0;
        form.Changed += () => changes++;

        form.SetErrors(new Dictionary<string, IReadOnlyList<string>> { ["email"] = ["Bad email."], [""] = ["Try again."] });

        Assert.Equal(["Bad email."], form.Errors("EMAIL"));
        Assert.Equal(["Try again.", "Bad email."], form.AllErrors());
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SelectOptionsReadFromStringsOrObjects()
    {
        var options = JsonSerializer.Deserialize<List<SelectOption>>("""["Red", { "text": "Dark blue", "value": "blue" }]""", ComponentParameters.JsonOptions)!;

        Assert.Equal([new SelectOption("Red", "Red"), new SelectOption("Dark blue", "blue")], options);
    }

    [Theory]
    [InlineData("/media/a.jpg", true)]
    [InlineData("https://example.com/a.jpg", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData(" data:image/png;base64,AA", false)]
    [InlineData("", false)]
    public void ComponentsEmitOnlySafeUrls(string url, bool safe) => Assert.Equal(safe, SafeUrl.IsSafe(url));
}

// Media by drive placement (docs/media.md, F1): component values and inserted bodies use the
// placement's /files URL, never a library path.
public sealed class MediaReferenceTests
{
    [Theory]
    [InlineData("01JABCDEF123", "files/01JABCDEF123")]
    [InlineData("/media/about-bg.jpg", "/media/about-bg.jpg")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("", null)]
    public void APlacementIdBecomesItsFilesUrl(string value, string? expected) =>
        Assert.Equal(expected, Crest.Components.Files.MediaReference.Url(value));

    [Fact]
    public void InsertedMediaIsAnImageOrALinkByPlacement()
    {
        var image = File("p1", "photo.jpg", "image/jpeg", alternativeText: "A \"view\"");
        var document = File("p2", "terms.pdf", "application/pdf");

        Assert.Equal("<img src=\"files/p1\" alt=\"A &quot;view&quot;\" />", Crest.Components.Editors.MediaSnippets.Html(image));
        Assert.Equal("<a href=\"files/p2\">terms.pdf</a>", Crest.Components.Editors.MediaSnippets.Html(document));
        Assert.Equal("![A \"view\"](files/p1)", Crest.Components.Editors.MediaSnippets.Markdown(image));
        Assert.Equal("[terms.pdf](files/p2)", Crest.Components.Editors.MediaSnippets.Markdown(document));
    }

    private static Crest.Components.Files.CrestFileItem File(string id, string name, string type, string? alternativeText = null) => new(
        id, "c-" + id, "folder", "drive", name, Crest.Components.Files.CrestFileKind.File, null, null, false,
        new Crest.Components.Files.CrestFileMetadata(null, null, alternativeText, null), 10, type, 1, true,
        DateTimeOffset.UnixEpoch, null, DateTimeOffset.UnixEpoch, null, []);
}
