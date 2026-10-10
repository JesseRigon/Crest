using Crest.Components.Modules;

// This library's pages and seam implementations belong to the member shell. Its staff-facing
// counterparts would live in a separate blazor-wasm/ library: a shell's router routes every
// page in the assemblies it is handed, so a bucket is an assembly property.
[assembly: CrestShell(CrestShells.Member)]
