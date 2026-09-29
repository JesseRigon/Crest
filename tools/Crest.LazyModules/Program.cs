using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

// Crest lazy modules (OrchardCore.Crest.Client/CrestLazyModules.targets): reads the WASM app's
// referenced assemblies and decides which load on demand. Arguments: references file (one
// "path|source project" per line), extra lazy names file, app assembly name, generated C#
// file, lazy list output file.
var referencesFile = args[0];
var extraLazy = File.Exists(args[1]) ? File.ReadAllLines(args[1]) : [];
var appAssembly = args[2];
var generatedFile = args[3];
var lazyListFile = args[4];
var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var line in File.ReadAllLines(referencesFile).Where(l => l.Length > 0))
{
    var parts = line.Split('|');
    var path = parts[0];
    if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
    var name = Path.GetFileNameWithoutExtension(path);
    byName[name] = path;
    var project = (parts.Length > 1 ? parts[1] : string.Empty).Replace('\\', '/');
    if (project.Contains("/blazor-wasm/")) moduleNames.Add(name);
}

var refs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
var routes = new List<KeyValuePair<string, string>>();
var jsComponentAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var regionAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            if (!moduleNames.Contains(pair.Key)) continue;

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
                    else if (attributeName == "Crest.Components.Modules.CrestJSComponentAttribute")
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
var lazyRoots = new HashSet<string>(moduleNames.Where(n => !jsComponentAssemblies.Contains(n)).Concat(extra), StringComparer.OrdinalIgnoreCase);
var eagerRoots = new[] { appAssembly }.Concat(jsComponentAssemblies);
var eager = closure(eagerRoots, lazyRoots);
Func<string, bool> isLazy = n => !isFramework(n) && !eager.Contains(n) && refs.ContainsKey(n);
var lazy = closure(lazyRoots, new HashSet<string>()).Where(isLazy).OrderBy(n => n).ToArray();
var lazySet = new HashSet<string>(lazy, StringComparer.OrdinalIgnoreCase);

File.WriteAllLines(lazyListFile, lazy.Select(n => n + ".wasm"));

Func<string, string> quote = s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
var code = new StringBuilder();
code.AppendLine("// <auto-generated/> CrestLazyModules.targets: module pages loaded on demand.");
code.AppendLine("namespace Crest.Client;");
code.AppendLine("internal static class CrestLazyModulesManifest");
code.AppendLine("{");
code.AppendLine("    public static Crest.Admin.CrestLazyModules Create() => new(");
code.AppendLine("        routes: new (string Template, string Assembly)[] {");
foreach (var route in routes.Where(r => lazySet.Contains(r.Value)).OrderBy(r => r.Key, StringComparer.Ordinal))
    code.AppendLine("            (" + quote(route.Key) + ", " + quote(route.Value) + "),");
code.AppendLine("        },");
code.AppendLine("        dependencies: new System.Collections.Generic.Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase) {");
foreach (var module in lazyRoots.Where(n => moduleNames.Contains(n) && lazySet.Contains(n)).OrderBy(n => n))
{
    var deps = closure(new[] { module }, new HashSet<string>(lazyRoots.Where(r => r != module), StringComparer.OrdinalIgnoreCase)).Where(lazySet.Contains).OrderBy(n => n);
    code.AppendLine("            [" + quote(module) + "] = new[] { " + string.Join(", ", deps.Select(quote)) + " },");
}
code.AppendLine("        },");
code.AppendLine("        regionContributors: new[] { " + string.Join(", ", regionAssemblies.Where(lazySet.Contains).OrderBy(n => n).Select(quote)) + " });");
code.AppendLine("}");
Directory.CreateDirectory(Path.GetDirectoryName(generatedFile)!);
if (!File.Exists(generatedFile) || File.ReadAllText(generatedFile) != code.ToString()) File.WriteAllText(generatedFile, code.ToString());
Console.WriteLine("Crest lazy modules: " + lazy.Length + " lazy assemblies, " + routes.Count + " routes, kept eager for JS components: " + string.Join(", ", jsComponentAssemblies));
