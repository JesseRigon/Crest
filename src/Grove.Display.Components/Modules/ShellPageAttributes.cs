namespace Grove.Display.Components.Modules;

/// <summary>
/// Declares that a page establishes a session: it sets the authentication cookie (a login,
/// a registration, a password reset that signs in), so it renders
/// <c>InteractiveWebAssembly</c> instead of <c>InteractiveAuto</c>.
/// </summary>
/// <remarks>
/// Under InteractiveAuto's first-visit server circuit a credential POST goes out on the
/// server-side loopback <c>HttpClient</c>, so the auth <c>Set-Cookie</c> lands in that handler
/// and never reaches the browser: a "successful" sign-in that leaves the browser anonymous.
/// InteractiveWebAssembly keeps the prerendered form and runs the interactive phase in the
/// browser, where the fetch's cookie is stored. The page declares this itself; the document
/// root reads it off the routed page, never off a list of shell paths
/// (docs/shells-and-themes.md › Auth-cookie pages render InteractiveWebAssembly).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SetsAuthenticationCookieAttribute : Attribute
{
}

/// <summary>
/// Declares a member page that is served at the member shell's base, outside any
/// organization: sign-in, registration, password reset, the organization picker.
/// </summary>
/// <remarks>
/// Every other member page carries the organization in its path,
/// <c>/{member prefix}/{organization slug}/…</c>, so a bookmark opens the right organization
/// and switching organizations is a navigation. The server's shell selector serves a page
/// with this marker only at the shell base and every other member page only under an
/// organization; the member router applies the same split on client-side navigation.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OrganizationFreePageAttribute : Attribute
{
}
