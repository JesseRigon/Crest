using Grove.Components.Modules;

// This theme client's pages belong to the member shell. Read by the server's route-table
// providers, the endpoint bucket stamping, the lazy-module generator and the shells'
// routers - see GroveShellAttribute for why a bucket is an assembly property.
// Routes: the shell root (Grove.Shell.GroveShellRoot) renders it once this lazy theme UI is loaded.
[assembly: GroveShell(GroveShells.Member, Routes = typeof(Grove.MemberTheme.MemberRoutes))]
