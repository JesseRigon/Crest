using Grove.Localization.PortableObject;

namespace Grove.Tests.Localization;

public class TranslationContextMapTests
{
    [Theory]
    [InlineData("OrchardCore.Users.Controllers.AccountController", "Grove.Users.Controllers.AccountController")]
    [InlineData("OrchardCore.Workflows.Activities.NotifyTask", "Grove.Workflows.Activities.NotifyTask")]
    [InlineData("OrchardCore.ReCaptcha.Workflows.ValidateReCaptchaTask", "Grove.ReCaptcha.Workflows.ValidateReCaptchaTask")]
    [InlineData("OrchardCore.DisplayManagement.IOrchardHelper", "Grove.DisplayManagement.IPlatformHelper")]
    [InlineData("OrchardCore", "Grove")]
    [InlineData("TheAdmin.Views.Layout", "TheAdmin.Views.Layout")]
    [InlineData("Grove.Users.Controllers.AccountController", "Grove.Users.Controllers.AccountController")]
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

        Assert.Equal("Grove.Users.Controllers.AccountController", record.Key.Context);
    }
}
