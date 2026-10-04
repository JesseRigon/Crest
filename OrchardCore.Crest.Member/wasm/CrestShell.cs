using Crest.Components.Modules;

// This theme client's pages belong to the member shell. Read by the server's route-table
// providers, the endpoint bucket stamping, the lazy-module generator and the shells'
// routers - see CrestShellAttribute for why a bucket is an assembly property.
[assembly: CrestShell(CrestShells.Member)]
