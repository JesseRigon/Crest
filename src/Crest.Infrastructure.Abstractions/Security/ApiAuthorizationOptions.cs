using Microsoft.AspNetCore.Authentication;

namespace Crest.Security;

public class ApiAuthorizationOptions : AuthenticationSchemeOptions
{
    /// <summary>The scheme the <c>Api</c> scheme forwards to first: a bearer token's.</summary>
    public string ApiAuthenticationScheme { get; set; } = "Bearer";

    /// <summary>
    /// Further API credential schemes the forwarder tries when the first yields nothing (a
    /// remote deployment key, an opaque machine key). Each is an authentication scheme a
    /// module registered; the first that authenticates wins. The challenge stays the bearer's.
    /// </summary>
    public IList<string> AdditionalSchemes { get; } = new List<string>();
}
