using Crest.Localization.PortableObject;

namespace Crest.Tests.Localization;

public class TranslationContextMapTests
{
    [Theory]
    [InlineData("OrchardCore.Users.Controllers.AccountController", "Crest.Users.Controllers.AccountController")]
    [InlineData("OrchardCore.Workflows.Activities.NotifyTask", "Crest.Workflows.Activities.NotifyTask")]
    [InlineData("OrchardCore.ReCaptcha.Workflows.ValidateReCaptchaTask", "Crest.ReCaptcha.Workflows.ValidateReCaptchaTask")]
    [InlineData("OrchardCore.DisplayManagement.IOrchardHelper", "Crest.DisplayManagement.IPlatformHelper")]
    [InlineData("OrchardCore", "Crest")]
    [InlineData("TheAdmin.Views.Layout", "TheAdmin.Views.Layout")]
    [InlineData("Crest.Users.Controllers.AccountController", "Crest.Users.Controllers.AccountController")]
    [InlineData(null, null)]
    public void MapsOriginalContextsToRenamedOnes(string context, string expected)
        => Assert.Equal(expected, TranslationContextMap.Map(context));

    [Fact]
    public void ParsedRecordsCarryTheMappedContext()
    {
        var po = """
            msgctxt "OrchardCore.Users.Controllers.AccountController"
            msgid "Log in"
            msgstr "Connexion"
            """;

        var record = PoParser.Parse(new StringReader(po)).Single();

        Assert.Equal("Crest.Users.Controllers.AccountController", record.Key.Context);
    }
}
