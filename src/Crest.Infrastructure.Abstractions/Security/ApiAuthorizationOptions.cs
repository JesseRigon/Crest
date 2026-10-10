using Microsoft.AspNetCore.Authentication;

namespace Crest.Security;

public class ApiAuthorizationOptions : AuthenticationSchemeOptions
{
    public string ApiAuthenticationScheme { get; set; } = "Bearer";
}
