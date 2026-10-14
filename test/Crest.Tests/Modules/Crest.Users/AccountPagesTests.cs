using Crest.Users.Services;

namespace Crest.Tests.Modules.Crest.Users;

// The account pages replacing the MVC account screens keep their paths (step 7), so the links the
// server sends - the email confirmation above all - are composed on the tenant's base.
public class AccountPagesTests
{
    [Fact]
    public void TheConfirmationLinkIsAbsoluteOnTheTenantBaseAndEncoded()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");
        context.Request.PathBase = "/t2";

        var url = AccountPages.ConfirmEmailUrl(context.Request, "user 1", "a+b/c=");

        Assert.Equal("https://example.com/t2/ConfirmEmail?userId=user%201&code=a%2Bb%2Fc%3D", url);
    }

    [Fact]
    public void TheAccountPagesKeepTheStockPaths()
    {
        Assert.Equal("/ConfirmEmail", AccountPages.ConfirmEmail);
        Assert.Equal("/ConfirmEmailSent", AccountPages.ConfirmEmailSent);
        Assert.Equal("/ChangePassword", AccountPages.ChangePassword);
        Assert.Equal("/ChangeEmail", AccountPages.ChangeEmail);
        Assert.Equal("/ExternalLogins", AccountPages.ExternalLogins);
    }
}
