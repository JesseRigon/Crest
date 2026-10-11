#!/usr/bin/env python3
"""Move contract files out of a module into its .Abstractions project.

    tools/extract-abstractions.py MODULE [--from-core] [--dry-run] [FILE ...]

MODULE is a module project name under src/ (for example Crest.Flows). FILE paths are
relative to the module folder. `--from-core` adds every file that an uncommitted fold of
MODULE.Core moved into the module (read from `git status`) and that is a contract by kind:
it declares an interface, is named *Permissions.cs, *Constants.cs, *Options.cs or
*Context.cs, or sits under Models/, ViewModels/, Settings/, Fields/, Indexes/, Model/,
Options/, Abstractions/ or Handlers/ without being a handler, driver or migration class.

For the chosen files the script:

1. Creates MODULE.Abstractions when it does not exist (root namespace MODULE), adds it to
   Crest.Platform.slnx next to the module and to the platform library list, and makes the
   module reference it.
2. Works out what the moved files need from their `using` lines: a namespace declared by a
   project other than the module becomes a project reference (abstractions projects
   preferred; a project that already depends on MODULE.Abstractions is refused as a cycle);
   a namespace nobody in src/ declares is matched to the longest package name in the
   central Directory.Packages.props that prefixes it, and becomes a package reference.
3. Drops a file that depends on code staying in the module (through a using, a global
   using of the module, or a type in a shared namespace), repeating until the set is
   stable, and reports each dropped file.
4. `git mv`s the files, keeping their folder layout and namespaces.

Nothing is committed. Dry run prints the plan and changes nothing.
"""

import collections
import glob
import json
import os
import re
import subprocess
import sys
from pathlib import Path

CONTRACT_DIRS = ("/Models/", "/ViewModels/", "/Settings/", "/Fields/", "/Indexes/", "/Model/", "/Options/", "/Abstractions/", "/Handlers/")
CONTRACT_SUFFIXES = ("Permissions.cs", "Constants.cs", "Options.cs", "Context.cs")
NS_RE = re.compile(r"^namespace\s+([\w.]+)\s*[;{]", re.M)
USING_RE = re.compile(r"^(?:global\s+)?using\s+(?!static)([\w.]+);", re.M)


def run(*args, cwd=None):
    return subprocess.run(args, check=True, capture_output=True, text=True, cwd=cwd).stdout


def is_contract(path, text):
    name = os.path.basename(path)
    if re.search(r"^\s*(?:public|internal)\s+(?:partial\s+)?interface\s", text, re.M):
        return True
    if name.endswith(CONTRACT_SUFFIXES):
        return True
    if any(d in "/" + path for d in CONTRACT_DIRS):
        return not re.search(r"\bclass\s+\w+(Handler|Driver|Migrations?)\b", text)
    return False


def main():
    flags = {a for a in sys.argv[1:] if a.startswith("--")}
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not args:
        sys.exit(__doc__)
    module, files = args[0], set(args[1:])
    dry, from_core = "--dry-run" in flags, "--from-core" in flags
    root = Path(run("git", "rev-parse", "--show-toplevel").strip())
    src = root / "src"
    mod_dir = src / module
    abs_name = f"{module}.Abstractions"
    abs_dir = src / abs_name
    if not mod_dir.is_dir():
        sys.exit(f"no module src/{module}")

    if from_core:
        for line in run("git", "status", "--short", cwd=root).splitlines():
            m = re.match(rf"^R[M ]?\s+src/{re.escape(module)}\.Core/.+?\.cs -> src/{re.escape(module)}/(.+\.cs)$", line)
            if m:
                rel = m.group(1)
                text = (mod_dir / rel).read_text(encoding="utf-8", errors="ignore")
                if is_contract(rel, text):
                    files.add(rel)
    files = {f for f in files if (mod_dir / f).is_file()}
    if not files:
        sys.exit("no files to move")

    # namespace -> declaring projects, over src/ (the Workflows engine is a separate world)
    nsidx = collections.defaultdict(set)
    for f in glob.glob(str(src / "**" / "*.cs"), recursive=True):
        if "/obj/" in f or "/bin/" in f or "/Crest.Workflows/" in f:
            continue
        try:
            text = open(f, encoding="utf-8", errors="ignore").read()
        except OSError:
            continue
        proj = Path(f).relative_to(src).parts[0]
        for n in NS_RE.findall(text):
            nsidx[n].add(proj)

    graph = json.load(open(root / "docs" / "dependencies.json"))
    edges = collections.defaultdict(set)
    for e in graph["edges"]:
        edges[e["from"]].add(e["to"])

    def reaches(a, b):
        seen, stack = set(), [a]
        while stack:
            x = stack.pop()
            for y in edges.get(x, ()):
                if y == b:
                    return True
                if y not in seen:
                    seen.add(y)
                    stack.append(y)
        return False

    packages = set()
    for f in root.glob("**/Directory.Packages.props"):
        if "node_modules" in f.parts:
            continue
        packages |= set(re.findall(r'PackageVersion Include="([^"]+)"', f.read_text(encoding="utf-8")))

    def package_for(ns):
        best = ""
        for p in packages:
            if (ns == p or ns.startswith(p + ".")) and len(p) > len(best):
                best = p
        if best:
            return best
        children = sorted((p for p in packages if p.startswith(ns + ".")), key=len)
        return children[0] if children else None

    # global usings in the module apply to every file it holds
    global_ns = set()
    for f in mod_dir.rglob("*.cs"):
        if "/obj/" in f.as_posix() or "/bin/" in f.as_posix():
            continue
        text = f.read_text(encoding="utf-8", errors="ignore")
        global_ns |= set(re.findall(r"^global\s+using\s+(?!static)([\w.]+);", text, re.M))

    # resolve, dropping files that depend on code staying in the module
    while True:
        moving_ns = set()
        for rel in files:
            moving_ns |= set(NS_RE.findall((mod_dir / rel).read_text(encoding="utf-8", errors="ignore")))
        existing_ns = {n for n, ps in nsidx.items() if abs_name in ps}
        need_projects, need_packages, dropped = set(), set(), {}
        for rel in sorted(files):
            text = (mod_dir / rel).read_text(encoding="utf-8", errors="ignore")
            for ns in set(USING_RE.findall(text)) | global_ns:
                if ns.startswith(("System", "Microsoft")) or ns in moving_ns or ns in existing_ns:
                    continue
                providers = nsidx.get(ns, set()) - {module, abs_name}
                if providers:
                    pick = sorted(providers, key=lambda p: (not p.endswith(".Abstractions"), p))[0]
                    if reaches(pick, abs_name):
                        dropped[rel] = f"{ns} comes from {pick}, which already depends on {abs_name}"
                        break
                    need_projects.add(pick)
                elif module in nsidx.get(ns, set()):
                    dropped[rel] = f"{ns} is declared only by code staying in {module}"
                    break
                else:
                    pkg = package_for(ns)
                    if pkg:
                        need_packages.add(pkg)
                    else:
                        dropped[rel] = f"{ns} is not declared in src/ and matches no central package"
                        break
        # same-namespace references need no using: a moving file that names a type declared
        # by a file staying in the module, in a namespace the moving file shares, stays too
        staying = {}
        for f in mod_dir.rglob("*.cs"):
            rel = f.relative_to(mod_dir).as_posix()
            if rel in files or "/obj/" in f.as_posix() or "/bin/" in f.as_posix():
                continue
            text = f.read_text(encoding="utf-8", errors="ignore")
            for ns in NS_RE.findall(text):
                for t in re.findall(r"^\s*(?:public|internal)\s+(?:static |sealed |abstract |partial )*(?:class|interface|record|struct|enum)\s+(\w+)", text, re.M):
                    staying.setdefault(t, set()).add(ns)
        for rel in sorted(files):
            if rel in dropped:
                continue
            text = (mod_dir / rel).read_text(encoding="utf-8", errors="ignore")
            scope = set(NS_RE.findall(text)) | set(USING_RE.findall(text)) | global_ns
            for t, nss in staying.items():
                if nss & scope and re.search(r"\b" + re.escape(t) + r"\b", text):
                    dropped[rel] = f"uses {t}, which stays in {module}"
                    break
        if not dropped:
            break
        for rel, why in dropped.items():
            print(f"skip {rel}: {why}")
            files.discard(rel)
        if not files:
            sys.exit("nothing left to move")

    abs_csproj = abs_dir / f"{abs_name}.csproj"
    create = not abs_csproj.exists()
    if create:
        xml = f"""<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>{module}</RootNamespace>
    <!-- NuGet properties-->
    <Title>{module.replace('.', ' ')} Abstractions</Title>
    <Description>$(OCCMSDescription)

    Abstractions for the {module.split('.')[-1]} module.</Description>
    <PackageTags>$(PackageTags) PlatformCMS Abstractions</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

</Project>
"""
    else:
        xml = abs_csproj.read_text(encoding="utf-8")
    have_p = set(re.findall(r'ProjectReference Include="[^"]*\\([^"\\]+)\.csproj"', xml))
    have_k = set(re.findall(r'PackageReference Include="([^"]+)"', xml))
    add_p = sorted(p for p in need_projects if p not in have_p)
    add_k = sorted(k for k in need_packages if k not in have_k)
    print(f"{'create' if create else 'update'} {abs_name}: move {len(files)} files; add projects {add_p}; add packages {add_k}")
    for rel in sorted(files):
        print(f"  {rel}")
    if dry:
        print("dry run; nothing changed")
        return

    def add_group(xml, lines):
        block = "".join(f"    {l}\n" for l in lines)
        return xml.replace("\n</Project>", f"\n  <ItemGroup>\n{block}  </ItemGroup>\n\n</Project>", 1)

    if add_k:
        xml = add_group(xml, [f'<PackageReference Include="{k}" />' for k in add_k])
    if add_p:
        xml = add_group(xml, [f'<ProjectReference Include="..\\{p}\\{p}.csproj" />' for p in add_p])
    abs_dir.mkdir(exist_ok=True)
    abs_csproj.write_text(xml, encoding="utf-8")
    for rel in sorted(files):
        target = abs_dir / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        run("git", "mv", str(mod_dir / rel), str(target), cwd=root)
    run("git", "add", str(abs_csproj), cwd=root)

    mod_csproj = mod_dir / f"{module}.csproj"
    mx = mod_csproj.read_text(encoding="utf-8")
    ref = f'<ProjectReference Include="..\\{abs_name}\\{abs_name}.csproj" />'
    if ref not in mx:
        m = re.search(r"(<ItemGroup>\n(?:\s*<ProjectReference\b[^\n]*\n)+)", mx)
        mx = mx[: m.end()] + f"    {ref}\n" + mx[m.end():] if m else add_group(mx, [ref])
        mod_csproj.write_text(mx, encoding="utf-8")
    if create:
        sln = root / "Crest.Platform.slnx"
        sx = sln.read_text(encoding="utf-8")
        anchor = f'    <Project Path="src/{module}/{module}.csproj" />'
        if anchor in sx and abs_name not in sx:
            sx = sx.replace(anchor, f'    <Project Path="src/{abs_name}/{abs_name}.csproj" />\n{anchor}', 1)
            sln.write_text(sx, encoding="utf-8")
        props = src / "Crest.Build" / "Platform.Projects.props"
        px = props.read_text(encoding="utf-8")
        if f";{abs_name};" not in px:
            px = px.replace("</PlatformLibraryProjects>", f"{abs_name};</PlatformLibraryProjects>", 1)
            props.write_text(px, encoding="utf-8")
    print(f"done: {abs_name}")


if __name__ == "__main__":
    main()
