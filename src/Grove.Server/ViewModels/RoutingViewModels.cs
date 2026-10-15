using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Grove;
using Grove.Middlewares;

namespace Grove.ViewModels;

// TenantBase is the tenant's browser-absolute base path ("" or "/t2"); the shell paths are
// relative to it. The client takes the tenant base from here and never derives it.
// EnabledModules lists the module client libraries (*.BlazorWasm) whose module is enabled for
// the tenant: the browser's lazy loader fetches only those, so a disabled module is absent
// client-side as it is on the server.
public sealed record RoutingResponse(string TenantBase, string AdminPath, string LoginPath, string MemberPath, IReadOnlyList<string> EnabledModules);
