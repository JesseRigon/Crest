using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

// Grove lazy modules (build/Grove.LazyModules.targets): reads the host WASM app's
// referenced assemblies and decides which load on demand. Arguments: references file (one
// assembly path per line), app assembly name (also the generated manifest's namespace),
// generated C# file, lazy list output file.
//
// Nothing depends on where a project sits on disk, so a module referenced as a NuGet package
// is treated exactly like one built from source:
// - a module client library is a *.BlazorWasm assembly - the same naming convention
//   Grove.Server's Startup uses to feed module pages to the server-side route table, so the
//   two runtimes cannot disagree about which assemblies are modules;
// - a module that loads libraries itself (the workflow designer) embeds their names as the
//   grove-lazy-assemblies.txt manifest resource. Those are libraries, not modules, even when
//   they are named *.BlazorWasm.
var referencesFile = args[0];
var appAssembly = args[1];
var generatedFile = args[2];
var lazyListFile = args[3];
var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var extraLazy = new List<string>();
foreach (var path in File.ReadAllLines(referencesFile).Select(l => l.Trim()).Where(l => l.Length > 0))
{
    if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
    var name = Path.GetFileNameWithoutExtension(path);
    byName[name] = path;
    if (name.EndsWith(".BlazorWasm", StringComparison.OrdinalIgnoreCase)) moduleNames.Add(name);
}

var refs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
var routes = new List<KeyValuePair<string, string>>();
var jsComponentAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var regionAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
// The shell each client assembly declares ([assembly: GroveShell(...)]). A module library
// declaring none is admin; theme clients always declare theirs.
var shellOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
const string GroveShellAttributeName = "Grove.Components.Modules.GroveShellAttribute";
// Assemblies that load at boot whatever shell the document serves (the shell runtime, a
// theme's services): [assembly: GroveEager].
const string GroveEagerAttributeName = "Grove.Components.Modules.GroveEagerAttribute";
var eagerMarked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
const string LazyAssembliesResourceName = "grove-lazy-assemblies.txt";

Func<MetadataReader, EntityHandle, string> attributeTypeName = (md, ctor) =>
{
    if (ctor.Kind == HandleKind.MemberReference)
    {
        var parent = md.GetMemberReference((MemberReferenceHandle)ctor).Parent;
        if (parent.Kind == HandleKind.TypeReference) { var t = md.GetTypeReference((TypeReferenceHandle)parent); return md.GetString(t.Namespace) + "." + md.GetString(t.Name); }
    }
    else if (ctor.Kind == HandleKind.MethodDefinition)
    {
        var t = md.GetTypeDefinition(md.GetMethodDefinition((MethodDefinitionHandle)ctor).GetDeclaringType());
        return md.GetString(t.Namespace) + "." + md.GetString(t.Name);
    }
    return string.Empty;
};

foreach (var pair in byName)
{
    try
    {
        using (var stream = File.OpenRead(pair.Value))
        using (var pe = new PEReader(stream))
        {
            if (!pe.HasMetadata) continue;
            var md = pe.GetMetadataReader();
            refs[pair.Key] = md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).ToArray();
            foreach (var attributeHandle in md.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = md.GetCustomAttribute(attributeHandle);
                var assemblyAttributeName = attributeTypeName(md, attribute.Constructor);
                if (assemblyAttributeName == GroveEagerAttributeName) { eagerMarked.Add(pair.Key); continue; }
                if (assemblyAttributeName != GroveShellAttributeName) continue;
                var blob = md.GetBlobReader(attribute.Value);
                if (blob.ReadUInt16() == 1 && blob.ReadSerializedString() is { } declaredShell) shellOf[pair.Key] = declaredShell;
            }
            if (!moduleNames.Contains(pair.Key)) continue;
            extraLazy.AddRange(ReadLazyAssembliesResource(pe, md));

            foreach (var typeHandle in md.TypeDefinitions)
            {
                var type = md.GetTypeDefinition(typeHandle);
                foreach (var attributeHandle in type.GetCustomAttributes())
                {
                    var attribute = md.GetCustomAttribute(attributeHandle);
                    var attributeName = attributeTypeName(md, attribute.Constructor);
                    if (attributeName == "Microsoft.AspNetCore.Components.RouteAttribute")
                    {
                        var blob = md.GetBlobReader(attribute.Value);
                        if (blob.ReadUInt16() == 1) routes.Add(new KeyValuePair<string, string>(blob.ReadSerializedString(), pair.Key));
                    }
                    else if (attributeName == "Grove.Components.Modules.GroveJSComponentAttribute")
                    {
                        jsComponentAssemblies.Add(pair.Key);
                    }
                }

                foreach (var implementationHandle in type.GetInterfaceImplementations())
                {
                    var iface = md.GetInterfaceImplementation(implementationHandle).Interface;
                    if (iface.Kind == HandleKind.TypeReference && md.GetString(md.GetTypeReference((TypeReferenceHandle)iface).Name) == "IPageRegionContributor")
                        regionAssemblies.Add(pair.Key);
                }
            }
        }
    }
    catch (BadImageFormatException) { }
}

Func<string, bool> isFramework = n => n.StartsWith("System.", StringComparison.Ordinal) || n.StartsWith("Microsoft.", StringComparison.Ordinal) || n == "System" || n == "netstandard" || n == "mscorlib";
Func<IEnumerable<string>, HashSet<string>, HashSet<string>> closure = (roots, stop) =>
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var queue = new Queue<string>(roots);
    while (queue.Count > 0)
    {
        var n = queue.Dequeue();
        if (stop.Contains(n) || !refs.ContainsKey(n) || !seen.Add(n)) continue;
        foreach (var r in refs[n]) queue.Enqueue(r);
    }
    return seen;
};

var extra = new HashSet<string>(extraLazy.Select(s => s.Trim()).Where(s => s.Length > 0), StringComparer.OrdinalIgnoreCase);
// A library a module loads itself is not a module of its own.
moduleNames.ExceptWith(extra);
// Theme clients (an assembly declaring its shell that is not a module) are lazy too: each
// theme UI loads when its shell's document boots (Grove.Shell.GroveShellRoot), so a browser
// on the public site never downloads the admin UI (docs/shells-and-themes.md › Theme UIs load
// per shell). One marked [GroveEager] stays eager.
var themeClients = shellOf.Keys.Where(n => !moduleNames.Contains(n) && !eagerMarked.Contains(n)).OrderBy(n => n).ToArray();
// A module library marked [GroveEager] stays eager too (the option picker's field editor, which
// every content editor needs before any of its routes loads).
var lazyRoots = new HashSet<string>(moduleNames.Where(n => !jsComponentAssemblies.Contains(n) && !eagerMarked.Contains(n)).Concat(extra).Concat(themeClients), StringComparer.OrdinalIgnoreCase);
// The eager roots: what boots. The app assembly itself cannot be one in practice: this runs
// before the app is compiled, so it is not among the references read above and a walk from
// it reaches nothing. So the boot path is named by [assembly: GroveEager] (the shell runtime,
// a theme's services - Program.cs calls into them), plus the JS-component modules.
var eagerRoots = new[] { appAssembly }.Concat(eagerMarked).Concat(jsComponentAssemblies);
var eager = closure(eagerRoots, lazyRoots);
Func<string, bool> isLazy = n => !isFramework(n) && !eager.Contains(n) && refs.ContainsKey(n);
var lazy = closure(lazyRoots, new HashSet<string>()).Where(isLazy).OrderBy(n => n).ToArray();
var lazySet = new HashSet<string>(lazy, StringComparer.OrdinalIgnoreCase);

File.WriteAllLines(lazyListFile, lazy.Select(n => n + ".wasm"));

Func<string, string> quote = s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
var code = new StringBuilder();
code.AppendLine("// <auto-generated/> Grove.LazyModules: module pages loaded on demand.");
code.AppendLine("namespace " + appAssembly + ";");
code.AppendLine("internal static class LazyModulesManifest");
code.AppendLine("{");
Func<string, string> shellOfModule = n => shellOf.TryGetValue(n, out var declared) ? declared : "admin";
code.AppendLine("    public static Grove.Shell.LazyModules Create() => new(");
// Every module client library, eager or lazy - the browser's module registry.
code.AppendLine("        modules: new string[] { " + string.Join(", ", moduleNames.OrderBy(n => n).Select(quote)) + " },");
code.AppendLine("        routes: new (string Template, string Assembly, string Shell)[] {");
foreach (var route in routes.Where(r => lazySet.Contains(r.Value)).OrderBy(r => r.Key, StringComparer.Ordinal))
    code.AppendLine("            (" + quote(route.Key) + ", " + quote(route.Value) + ", " + quote(shellOfModule(route.Value)) + "),");
code.AppendLine("        },");
// Every lazy module library, grouped by the shell it belongs to - what a shell loads when it
// needs all of its modules at once (the member shell, to activate the seams they implement).
code.AppendLine("        shellModules: new System.Collections.Generic.Dictionary<string, string[]>(System.StringComparer.Ordinal) {");
foreach (var group in lazyRoots.Where(n => moduleNames.Contains(n) && lazySet.Contains(n)).GroupBy(shellOfModule).OrderBy(g => g.Key, StringComparer.Ordinal))
    code.AppendLine("            [" + quote(group.Key) + "] = new string[] { " + string.Join(", ", group.OrderBy(n => n).Select(quote)) + " },");
code.AppendLine("        },");
code.AppendLine("        dependencies: new System.Collections.Generic.Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase) {");
foreach (var module in lazyRoots.Where(n => (moduleNames.Contains(n) || themeClients.Contains(n, StringComparer.OrdinalIgnoreCase)) && lazySet.Contains(n)).OrderBy(n => n))
{
    var deps = closure(new[] { module }, new HashSet<string>(lazyRoots.Where(r => r != module), StringComparer.OrdinalIgnoreCase)).Where(lazySet.Contains).OrderBy(n => n);
    code.AppendLine("            [" + quote(module) + "] = new string[] { " + string.Join(", ", deps.Select(quote)) + " },");
}
code.AppendLine("        },");
// Explicitly typed: with no module contributing regions - a host that enables no
// region-contributing module, or Grove on its own - an implicitly-typed empty array
// does not compile.
code.AppendLine("        regionContributors: new string[] { " + string.Join(", ", regionAssemblies.Where(lazySet.Contains).OrderBy(n => n).Select(quote)) + " },");
// Each shell's lazy theme UI, loaded when that shell's document boots.
code.AppendLine("        themes: new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal) {");
foreach (var group in themeClients.Where(lazySet.Contains).GroupBy(n => shellOf[n]).OrderBy(g => g.Key, StringComparer.Ordinal))
    code.AppendLine("            [" + quote(group.Key) + "] = " + quote(group.First()) + ",");
code.AppendLine("        });");
code.AppendLine("}");
Directory.CreateDirectory(Path.GetDirectoryName(generatedFile)!);
if (!File.Exists(generatedFile) || File.ReadAllText(generatedFile) != code.ToString()) File.WriteAllText(generatedFile, code.ToString());
Console.WriteLine("Grove lazy modules: " + lazy.Length + " lazy assemblies, " + routes.Count + " routes, lazy theme UIs: " + string.Join(", ", themeClients.Where(lazySet.Contains)) + ", kept eager for JS components: " + string.Join(", ", jsComponentAssemblies));

// The grove-lazy-assemblies.txt manifest resource a module embeds, one assembly name per line.
static IEnumerable<string> ReadLazyAssembliesResource(PEReader pe, MetadataReader md)
{
    foreach (var handle in md.ManifestResources)
    {
        var resource = md.GetManifestResource(handle);
        if (!resource.Implementation.IsNil || md.GetString(resource.Name) != LazyAssembliesResourceName) continue;
        var directory = pe.PEHeaders.CorHeader!.ResourcesDirectory;
        if (!pe.PEHeaders.TryGetDirectoryOffset(directory, out _)) yield break;
        var data = pe.GetSectionData(directory.RelativeVirtualAddress + (int)resource.Offset);
        var reader = data.GetReader();
        var length = reader.ReadInt32();
        var text = Encoding.UTF8.GetString(reader.ReadBytes(length));
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return line;
    }
}
