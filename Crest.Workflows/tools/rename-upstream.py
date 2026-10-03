#!/usr/bin/env python3
"""Rename the vendored Elsa identifiers to Crest.Workflows.

Run from the Crest repo root. Applies to identifiers in every non-doc text file under
Crest.Workflows (plus the host files listed in HOST_FILES), and to
file and directory names. Docs (*.md) are never touched: they are edited by hand.
Re-run it on a freshly copied upstream diff to map an upstream fix into the fork.

Rules, in order (see plans/workflows.md for the ruling):
  1. the meta project `Elsa` -> `Crest.Workflows.Engine` (paths, project refs, PackageId)
  2. `Elsa.Workflows.X`      -> `Crest.Workflows.X`
  3. `Elsa.X`                -> `Crest.Workflows.X`      (Common, Studio, Http, Api, ...)
  4. `namespace Elsa` / `using Elsa` / `global::Elsa` / the "Elsa" activity-namespace literal
                             -> `Crest.Workflows`
  5. identifiers prefixed `Elsa` (ElsaCollections, IElsaClient) -> `CrestWorkflows...`
Second pass (rules 6-8): upstream URLs -> the fork, the word Elsa / Elsa Workflows in prose,
Elsa_ collection prefixes, and lowercase elsa (route prefix, css classes, file names, camelCase).
"""
import os, re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[4]
MODULE = ROOT / 'Crest.Workflows'
HOST_FILES = []  # the consuming host repo updates its own solution, recipes and settings
TEXT_EXT = {'.cs', '.csproj', '.props', '.targets', '.razor', '.cshtml', '.js', '.ts', '.css', '.scss', '.json', '.xml', '.sln', '.slnx', '.txt', '.yml', '.yaml', '.html', '.editorconfig', '.ruleset', '.resx', '.mjs', '.cjs', '.tsx', '.jsx'}
SKIP_NAMES = {'LICENSE', 'UPSTREAM-COMMIT', 'rename-upstream.py'}
SKIP_DIRS = {'bin', 'obj', 'node_modules', '.git'}

def map_text(s: str) -> str:
    # 1. meta project (paths and ids) before the generic dotted rule sees it
    s = re.sub(r'(?<![\w.])Elsa[\\/]Elsa\.csproj', lambda m: m.group(0)[0:0] + 'Crest.Workflows.Engine' + m.group(0)[4:5] + 'Crest.Workflows.Engine.csproj', s)
    s = re.sub(r'(src[\\/]modules[\\/])Elsa(?=[\\/])', r'\1Crest.Workflows.Engine', s)
    s = s.replace('<PackageId>Elsa</PackageId>', '<PackageId>Crest.Workflows.Engine</PackageId>')
    s = re.sub(r'(Include|Update)="Elsa"', r'\1="Crest.Workflows.Engine"', s)
    # 2 + 3. dotted identifiers
    s = re.sub(r'\bElsa\.Workflows\b', 'Crest.Workflows', s)
    s = re.sub(r'\bElsa\.(?=[A-Z_])', 'Crest.Workflows.', s)
    # 4. bare root namespace in code positions and the activity-namespace literal
    s = re.sub(r'\bnamespace Elsa\b', 'namespace Crest.Workflows', s)
    s = re.sub(r'\busing Elsa;', 'using Crest.Workflows;', s)
    s = re.sub(r'\bglobal::Elsa\b', 'global::Crest.Workflows', s)
    s = re.sub(r'\[Activity\(\s*"Elsa"', '[Activity("Crest.Workflows"', s)
    s = re.sub(r'\bActivityTypeNameHelper\.GenerateTypeName\("Elsa"', 'ActivityTypeNameHelper.GenerateTypeName("Crest.Workflows"', s)
    # 5. Elsa-prefixed identifiers
    s = re.sub(r'\bElsa(?=[A-Z])', 'CrestWorkflows', s)
    s = re.sub(r'\b(I)Elsa(?=[A-Z])', r'\1CrestWorkflows', s)
    # 6. (second pass, 2026-09-28) upstream URLs -> the fork's home; whole strings first
    s = re.sub(r'https?://(www\.)?github\.com/elsa-workflows/[A-Za-z0-9._-]+', 'https://github.com/JesseRigon/OrchardCore.Crest', s)
    s = re.sub(r'https?://[a-z0-9.]*elsaworkflows\.io[^\s"\'<>)]*', 'https://github.com/JesseRigon/OrchardCore.Crest/blob/main/plans/workflows.md', s)
    s = re.sub(r'https?://f\.feedz\.io/elsa-workflows/[^\s"\'<>)]*', 'https://api.nuget.org/v3/index.json', s)
    # 7. prose and remaining words: "Elsa Workflows" -> "Crest Workflows", the word Elsa -> Crest.Workflows
    s = re.sub(r'\bElsa Workflows\b', 'Crest Workflows', s)
    s = re.sub(r'\bElsa_', 'CrestWorkflows_', s)
    s = re.sub(r'\bElsa\b(?!\w)(?!\.[A-Za-z_])', 'Crest.Workflows', s)
    # Elsa embedded in identifiers after a lowercase letter: AddElsa, ConfigureElsa, AddElsaDesigner
    s = re.sub(r'(?<=[a-z0-9])Elsa(?=[A-Z]|\b)', 'CrestWorkflows', s)
    # 8. lowercase tokens: route prefix, css classes, file names, camelCase identifiers
    s = re.sub(r'\belsa/api\b', 'crest-workflows/api', s)
    s = re.sub(r'\belsa(?=-)', 'crest-workflows', s)
    s = re.sub(r'\belsa(?=_)', 'crest_workflows', s)
    s = re.sub(r'\belsa(?=[A-Z])', 'crestWorkflows', s)
    # a bare lowercase elsa is an identifier (lambda parameters, JS variables, JSON keys)
    s = re.sub(r'\belsa\b', 'crestWorkflows', s)
    return s

def map_name(name: str) -> str:
    if name == 'Elsa.csproj': return 'Crest.Workflows.Engine.csproj'
    n = re.sub(r'^Elsa\.Workflows(?=\.|$)', 'Crest.Workflows', name)
    n = re.sub(r'^Elsa\.(?=[A-Z_])', 'Crest.Workflows.', n)
    n = re.sub(r'^Elsa(?=[A-Z])', 'CrestWorkflows', n)
    n = re.sub(r'^IElsa(?=[A-Z])', 'ICrestWorkflows', n)
    n = re.sub(r'^elsa(?=[-_.])', 'crest-workflows', n)
    n = re.sub(r'^elsa(?=[A-Z])', 'crestWorkflows', n)
    return n

def iter_files(base: pathlib.Path):
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames:
            yield pathlib.Path(dirpath) / f

def main(dry=False):
    touched = renamed = 0
    files = [p for p in iter_files(MODULE)] + [p for p in HOST_FILES if p.exists()]
    for p in files:
        if p.name in SKIP_NAMES or p.suffix == '.md': continue
        if p.suffix.lower() not in TEXT_EXT and p.name != '.gitignore' and p.name != 'FodyWeavers.xml': continue
        try:
            s = p.read_text(encoding='utf-8')
        except UnicodeDecodeError:
            continue
        n = map_text(s)
        if n != s:
            touched += 1
            if not dry: p.write_text(n, encoding='utf-8')
    # names: deepest first so parents rename after children
    entries = sorted(iter_paths(MODULE), key=lambda q: len(q.parts), reverse=True)
    for q in entries:
        new = map_name(q.name)
        if new != q.name:
            renamed += 1
            if not dry: q.rename(q.with_name(new))
    # the meta project folder: src/modules/Elsa -> Crest.Workflows.Engine
    meta = MODULE / 'engine/src/modules/Elsa'
    if meta.exists():
        renamed += 1
        if not dry: meta.rename(meta.with_name('Crest.Workflows.Engine'))
    print(f'{"would touch" if dry else "touched"} {touched} files, {"would rename" if dry else "renamed"} {renamed} paths')

def iter_paths(base: pathlib.Path):
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames: yield pathlib.Path(dirpath) / f
        for d in dirnames: yield pathlib.Path(dirpath) / d

if __name__ == '__main__':
    main(dry='--dry-run' in sys.argv)
