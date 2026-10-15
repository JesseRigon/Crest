using Grove.Display.Components.Modules;

// This theme client's pages belong to the admin shell. Read by the server's route-table
// providers, the endpoint bucket stamping, the lazy-module generator and the shells'
// routers - see GroveShellAttribute for why a bucket is an assembly property.
// Routes: the shell root (Grove.Infrastructure.Shell.GroveShellRoot) renders it once this lazy theme UI is loaded.
[assembly: GroveShell(GroveShells.Admin, Routes = typeof(Grove.Themes.Admin.AdminRoutes))]
