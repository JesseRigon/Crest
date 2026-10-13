using Crest.Members.Member.Services;
using Xunit;

namespace Crest.Members.Tests;

// Member self-registration shows the refusal's messages, never its raw body (the host
// bugs list, #1, client side).
public sealed class RegistrationRefusalTests
{
    [Theory]
    [InlineData("""{"errors":["An organization is required."]}""", "An organization is required.")]
    [InlineData("""{"errors":["Name taken.","Weak password."],"redirect":null}""", "Name taken. Weak password.")]
    [InlineData("""{"title":"One or more validation errors occurred.","errors":{"FirstName":["The FirstName field is required."]}}""", "The FirstName field is required.")]
    [InlineData("""{"title":"Bad"}""", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    public void RefusalMessageReadsTheErrors(string body, string? expected) =>
        Assert.Equal(expected, MemberAuthenticationService.RefusalMessage(body));
}
