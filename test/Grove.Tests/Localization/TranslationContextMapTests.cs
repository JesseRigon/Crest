using Grove.Data.Localization.PortableObject;

namespace Grove.Tests.Localization;

public class TranslationContextMapTests
{
    [Theory]
    [InlineData("OrchardCore.Users.Controllers.AccountController", "Grove.Access.Users.Controllers.AccountController")]
    [InlineData("OrchardCore.Workflows.Activities.NotifyTask", "Grove.Workflows.Activities.NotifyTask")]
    [InlineData("OrchardCore.ReCaptcha.Workflows.ValidateReCaptchaTask", "Grove.Security.ReCaptcha.Workflows.ValidateReCaptchaTask")]
    [InlineData("OrchardCore.DisplayManagement.IOrchardHelper", "Grove.Display.Management.IPlatformHelper")]
    [InlineData("OrchardCore", "Grove")]
    [InlineData("Grove.Themes.TheAdmin.Views.Layout", "Grove.Themes.TheAdmin.Views.Layout")]
    [InlineData("Grove.Access.Users.Controllers.AccountController", "Grove.Access.Users.Controllers.AccountController")]
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

        Assert.Equal("Grove.Access.Users.Controllers.AccountController", record.Key.Context);
    }
}
