#!/usr/bin/env python3
"""Fold one project into another.

    tools/fold-project.py OLD INTO [--dry-run] [--ns=NAMESPACE]

OLD and INTO are project names under src/ (for example Crest.Rules.Core and Crest.Rules).
Run from anywhere inside the repository. The script:

1. Moves every tracked file of OLD into INTO at the same relative path (`git mv`).
   A file that already exists in INTO is handled only for the known boilerplate cases:
   `Properties/AssemblyInfo.cs` and `GlobalUsings.cs` from OLD are dropped (an
   InternalsVisibleTo in OLD's AssemblyInfo is reported so it can be carried by hand).
   Any other collision aborts before anything changes.
2. Merges OLD's project file into INTO's: project, package and framework references and
   InternalsVisibleTo entries that INTO lacks are added; references to INTO itself and to
   OLD are dropped. Other elements in OLD's project file are reported, not merged.
3. Repoints every project file, props file and solution that referenced OLD to INTO,
   removing the reference where the file already references INTO or is INTO itself.
   OLD is removed from the project lists in Platform.Projects.props and from
   Directory.Packages.props (renamed there if INTO has no entry).
4. Renames the namespace OLD to INTO (or to --ns=NAMESPACE) in code: `namespace`, `using` and qualified
   references outside string literals, and InternalsVisibleTo("OLD") attributes and
   items. String literals are left alone on purpose: feature ids such as
   "Crest.Roles.Core" are tenant data, not project names.
5. Deletes what is left of OLD, build output included.

Nothing is committed. Dry run prints the plan and changes nothing.
"""

import re
import shutil
import subprocess
import sys
from pathlib import Path

TEXT_SUFFIXES = {".cs", ".cshtml", ".razor"}
SKIP_PARTS = {"bin", "obj", ".git", "node_modules"}
DROPPABLE = {"Properties/AssemblyInfo.cs", "GlobalUsings.cs"}


def run(*args, cwd=None):
    return subprocess.run(args, check=True, capture_output=True, text=True, cwd=cwd).stdout


def tracked(root, folder):
    out = run("git", "ls-files", "-z", "--", str(folder), cwd=root).split("\0")
    return [root / p for p in out if p]


def items(xml, tag):
    """Return (full element text, Include value) for every <tag Include=...> element."""
    found = []
    for m in re.finditer(rf"<{tag}\b[^>]*?(?:/>|>.*?</{tag}>)", xml, re.S):
        inc = re.search(r'Include="([^"]+)"', m.group(0))
        if inc:
            found.append((m.group(0), inc.group(1)))
    return found


def add_items(xml, tag, elements):
    """Append elements to the first ItemGroup holding <tag>, or a new ItemGroup."""
    if not elements:
        return xml
    block = "".join(f"    {e}\n" for e in elements)
    m = re.search(rf"(<ItemGroup>\n(?:\s*<{tag}\b[^\n]*\n)+)", xml)
    if m:
        return xml[: m.end()] + block + xml[m.end():]
    return xml.replace("\n</Project>", f"\n  <ItemGroup>\n{block}  </ItemGroup>\n\n</Project>", 1)


def outside_strings(line, fn):
    parts = line.split('"')
    for i in range(0, len(parts), 2):
        parts[i] = fn(parts[i])
    return '"'.join(parts)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    dry = "--dry-run" in sys.argv
    ns_opt = [a[5:] for a in sys.argv[1:] if a.startswith("--ns=")]
    if len(args) != 2:
        sys.exit(__doc__)
    old, into = args
    ns_new = ns_opt[0] if ns_opt else into  # --ns=X renames the namespace OLD to X instead of INTO
    root = Path(run("git", "rev-parse", "--show-toplevel").strip())
    src = root / "src"
    old_dir, into_dir = src / old, src / into
    if not old_dir.is_dir() or not into_dir.is_dir():
        sys.exit(f"both src/{old} and src/{into} must exist")
    old_csproj, into_csproj = old_dir / f"{old}.csproj", into_dir / f"{into}.csproj"

    # --- 1. plan the file moves -------------------------------------------------------
    moves, drops = [], []
    for f in tracked(root, old_dir):
        rel = f.relative_to(old_dir)
        if f == old_csproj:
            continue
        target = into_dir / rel
        if target.exists():
            if rel.as_posix() in DROPPABLE:
                drops.append(f)
                if "InternalsVisibleTo" in f.read_text(encoding="utf-8", errors="ignore"):
                    print(f"NOTE {f.relative_to(root)} carries InternalsVisibleTo; dropped, carry by hand if still needed")
            else:
                sys.exit(f"collision: {rel} exists in both {old} and {into}; resolve first")
        else:
            moves.append((f, target))
    print(f"{len(moves)} files to move, {len(drops)} boilerplate files to drop")

    # --- 2. merge the project file ------------------------------------------------------
    oxml = old_csproj.read_text(encoding="utf-8")
    ixml = into_csproj.read_text(encoding="utf-8")
    own = {old_csproj.name, into_csproj.name}
    adds = {}
    for tag in ("ProjectReference", "PackageReference", "FrameworkReference", "InternalsVisibleTo"):
        have = {inc for _, inc in items(ixml, tag)}
        new = []
        for el, inc in items(oxml, tag):
            if inc in have or inc.replace("\\", "/").rsplit("/", 1)[-1] in own or inc in (old, into):
                continue
            have.add(inc)
            new.append(el)
        adds[tag] = new
    known = {"Project", "PropertyGroup", "ItemGroup", "RootNamespace", "Title", "Description", "PackageTags",
             "ProjectReference", "PackageReference", "FrameworkReference", "InternalsVisibleTo"}
    others = sorted(set(re.findall(r"<(\w+)[\s>/]", oxml)) - known)
    if others:
        print(f"NOTE elements in {old}.csproj not merged: {others}")
    for tag, new in adds.items():
        for el in new:
            print(f"add to {into}.csproj: {el.strip()}")
    ixml2 = ixml
    for tag, new in adds.items():
        ixml2 = add_items(ixml2, tag, new)
    ixml2 = re.sub(rf'\s*<ProjectReference Include="[^"]*{re.escape(old)}\.csproj" />', "", ixml2)
    ixml2 = re.sub(rf'\s*<InternalsVisibleTo Include="{re.escape(old)}" />', "", ixml2)

    # --- 3. repoint referencers -----------------------------------------------------------
    ref_re = re.compile(rf'(?P<pre>[^"\s]*?){re.escape(old)}[\\/]{re.escape(old)}\.csproj')
    repoint = []
    for f in [root / p for p in run("git", "ls-files", "-z", cwd=root).split("\0") if p]:
        if f.suffix not in {".csproj", ".props", ".targets", ".slnx"} or f in (old_csproj, into_csproj):
            continue
        if any(part in SKIP_PARTS for part in f.parts):
            continue
        text = f.read_text(encoding="utf-8")
        if old not in text:
            continue
        new = text
        if f.name == "Platform.Projects.props":
            new = new.replace(f";{old};", ";")
        elif f.name == "Directory.Packages.props":
            if f'Include="{into}"' in new:
                new = re.sub(rf'\s*<PackageVersion Include="{re.escape(old)}" [^\n]*', "", new)
            else:
                new = new.replace(f'Include="{old}"', f'Include="{into}"')
        elif f.suffix == ".slnx":
            if f"/{into}/{into}.csproj" in new:
                new = re.sub(rf'\s*<Project Path="[^"]*/{re.escape(old)}/{re.escape(old)}\.csproj" />', "", new)
            else:
                new = new.replace(f"/{old}/{old}.csproj", f"/{into}/{into}.csproj")
        else:
            already = re.search(rf'{re.escape(into)}[\\/]{re.escape(into)}\.csproj', new) is not None
            if already:
                new = re.sub(rf'\s*<ProjectReference Include="[^"]*{re.escape(old)}[\\/]{re.escape(old)}\.csproj" />', "", new)
            else:
                new = ref_re.sub(lambda m: f"{m.group('pre')}{into}\\{into}.csproj" if "\\" in m.group(0) else f"{m.group('pre')}{into}/{into}.csproj", new)
            new = new.replace(f'<InternalsVisibleTo Include="{old}" />', f'<InternalsVisibleTo Include="{into}" />')
        if new != text:
            repoint.append((f, new))
    print(f"{len(repoint)} reference files to update")

    # --- 4. namespace rename in code --------------------------------------------------------
    token = re.compile(rf"(?<![\w.]){re.escape(old)}(?=[\s;.)\],>]|$)")
    code = []
    for f in [root / p for p in run("git", "ls-files", "-z", cwd=root).split("\0") if p]:
        if f.suffix not in TEXT_SUFFIXES or any(part in SKIP_PARTS for part in f.parts):
            continue
        try:
            text = f.read_text(encoding="utf-8")
        except (UnicodeDecodeError, FileNotFoundError):
            continue
        if old not in text:
            continue
        lines = text.split("\n")
        out = []
        for line in lines:
            line = line.replace(f'InternalsVisibleTo("{old}")', f'InternalsVisibleTo("{into}")')
            out.append(outside_strings(line, lambda s: token.sub(ns_new, s)))
        new = "\n".join(out)
        if new != text:
            code.append((f, new))
    print(f"{len(code)} code files with namespace changes")

    if dry:
        print("dry run; nothing changed")
        return

    # --- apply ----------------------------------------------------------------------------
    for f, target in moves:
        target.parent.mkdir(parents=True, exist_ok=True)
        run("git", "mv", str(f), str(target), cwd=root)
    for f in drops:
        run("git", "rm", "-q", str(f), cwd=root)
    into_csproj.write_text(ixml2, encoding="utf-8")
    for f, new in repoint:
        f.write_text(new, encoding="utf-8")
    run("git", "rm", "-q", "-f", str(old_csproj), cwd=root)
    # code files may have moved: resolve their new paths
    for f, new in code:
        if not f.exists():
            try:
                f = into_dir / f.relative_to(old_dir)
            except ValueError:
                continue
        f.write_text(new, encoding="utf-8")
    leftovers = tracked(root, old_dir)
    if leftovers:
        sys.exit(f"tracked files left in {old}: {[str(p.relative_to(root)) for p in leftovers]}")
    shutil.rmtree(old_dir, ignore_errors=True)
    print(f"folded {old} into {into}")


if __name__ == "__main__":
    main()
