using Grove.Users.Services;

namespace Grove.Tests.Modules.Grove.Users;

// The account pages keep the paths the account screens have always had, so the links the server
// sends - the email confirmation and the password reset above all - are composed on the tenant's base.
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
        Assert.Equal("/Register", AccountPages.Register);
        Assert.Equal("/RegistrationPending", AccountPages.RegistrationPending);
        Assert.Equal("/ForgotPassword", AccountPages.ForgotPassword);
        Assert.Equal("/ForgotPasswordConfirmation", AccountPages.ForgotPasswordConfirmation);
        Assert.Equal("/ResetPassword", AccountPages.ResetPassword);
        Assert.Equal("/ResetPasswordConfirmation", AccountPages.ResetPasswordConfirmation);
        Assert.Equal("/TwoFactor", AccountPages.TwoFactor);
        Assert.Equal("/LoginWithTwoFactorAuthentication", AccountPages.LoginWithTwoFactor);
    }

    [Fact]
    public void TheResetLinkCarriesTheCodeOnTheTenantBase()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");
        context.Request.PathBase = "/t2";

        var url = AccountPages.Url(context.Request, AccountPages.ResetPassword, ("code", "a+b/c="));

        Assert.Equal("https://example.com/t2/ResetPassword?code=a%2Bb%2Fc%3D", url);
    }
}
