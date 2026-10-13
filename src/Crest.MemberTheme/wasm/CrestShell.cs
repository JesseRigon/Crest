using Crest.Components.Modules;

// This theme client's pages belong to the member shell. Read by the server's route-table
// providers, the endpoint bucket stamping, the lazy-module generator and the shells'
// routers - see CrestShellAttribute for why a bucket is an assembly property.
// Routes: the shell root (Crest.Shell.CrestShellRoot) renders it once this lazy theme UI is loaded.
[assembly: CrestShell(CrestShells.Member, Routes = typeof(Crest.MemberTheme.MemberRoutes))]
