#!/usr/bin/env python3
"""List every feature id declared by the module and theme manifests under src/.

A manifest (Manifest.cs) declares a module or a theme ([assembly: Module(...)] /
[assembly: Theme(...)]) and optionally features ([assembly: Feature(...)]). When an assembly
declares at least one Feature, the module's own default feature is not a feature; otherwise
the module id (its Id, or the assembly name) is. Ids written as constants
(UserConstants.Features.Users) are resolved from the const string declarations under src/.

Usage:
  tools/manifest-features.py                 every feature id, one per line
  tools/manifest-features.py --json          ids with their flags (dependency-only, default
                                             tenant only, always enabled, dependencies)
  tools/manifest-features.py --check RECIPE  compare a recipe's feature step with the
                                             manifests: every id must be enabled or listed in
                                             the recipe's "excludedFeatures" (with a reason);
                                             exits 1 on a difference.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "src"

ATTRIBUTE = re.compile(r"\[assembly:\s*(Module|Theme|Feature)\s*\(", re.MULTILINE)
STRING = re.compile(r'^"((?:[^"\\]|\\.)*)"$')


def skip(path):
    parts = path.parts
    return "obj" in parts or "bin" in parts or "tests" in parts or "node_modules" in parts


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    return re.sub(r"(?m)//.*$", "", text)


def attribute_bodies(text):
    """Yield (kind, body) for each [assembly: X(...)] with balanced parentheses."""
    for match in ATTRIBUTE.finditer(text):
        depth, start = 1, match.end()
        i = start
        in_string = False
        while i < len(text) and depth:
            c = text[i]
            if in_string:
                if c == "\\":
                    i += 1
                elif c == '"':
                    in_string = False
            elif c == '"':
                in_string = True
            elif c == "(":
                depth += 1
            elif c == ")":
                depth -= 1
            i += 1
        yield match.group(1), text[start:i - 1]


def split_top_level(body):
    """Split 'A = x, B = [y, z]' on top-level commas."""
    parts, depth, current, in_string = [], 0, [], False
    i = 0
    while i < len(body):
        c = body[i]
        if in_string:
            current.append(c)
            if c == "\\":
                current.append(body[i + 1])
                i += 1
            elif c == '"':
                in_string = False
        elif c == '"':
            in_string = True
            current.append(c)
        elif c in "([{":
            depth += 1
            current.append(c)
        elif c in ")]}":
            depth -= 1
            current.append(c)
        elif c == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(c)
        i += 1
    if "".join(current).strip():
        parts.append("".join(current).strip())
    return parts


_constants = None


def constants():
    """Map 'Outer.Inner.Name' suffixes to string values from every const string under src/."""
    global _constants
    if _constants is not None:
        return _constants
    _constants = {}
    declaration = re.compile(r'(?:const\s+string|static\s+readonly\s+string)\s+(\w+)\s*=\s*"([^"]*)"')
    for path in SRC.rglob("*.cs"):
        if skip(path):
            continue
        text = strip_comments(path.read_text(encoding="utf-8", errors="ignore"))
        # Track class nesting by brace depth to qualify each constant.
        stack = []  # (name, depth at which its body opened)
        depth = 0
        pending_class = None
        token = re.compile(r'\b(?:class|struct|interface|record)\s+(\w+)|\{|\}|' + declaration.pattern + r'|"(?:[^"\\]|\\.)*"')
        for m in token.finditer(text):
            s = m.group(0)
            if m.group(1):
                pending_class = m.group(1)
            elif s == "{":
                depth += 1
                if pending_class:
                    stack.append((pending_class, depth))
                    pending_class = None
            elif s == "}":
                if stack and stack[-1][1] == depth:
                    stack.pop()
                depth -= 1
            elif m.group(2):
                qualified = [name for name, _ in stack] + [m.group(2)]
                for k in range(len(qualified)):
                    _constants.setdefault(".".join(qualified[k:]), set()).add(m.group(3))
    return _constants


def resolve(expression, where):
    expression = expression.strip()
    if expression.startswith("nameof("):
        return expression[len("nameof("):-1].split(".")[-1]
    m = STRING.match(expression)
    if m:
        return m.group(1)
    values = constants().get(expression)
    if values and len(values) == 1:
        return next(iter(values))
    raise SystemExit(f"{where}: cannot resolve '{expression}' ({sorted(values) if values else 'no declaration'})")


def resolve_list(expression, where):
    expression = expression.strip()
    if expression.startswith("new"):
        expression = expression[expression.index("{"):] if "{" in expression else expression
    if expression[:1] in "[{":
        items = split_top_level(expression[1:-1])
        return [resolve(item, where) for item in items if item]
    value = resolve(expression, where)
    return [v for v in re.split(r"[;, ]", value) if v]


def assembly_name(manifest):
    for directory in [manifest.parent, *manifest.parents]:
        projects = list(directory.glob("*.csproj"))
        if projects:
            text = projects[0].read_text(encoding="utf-8")
            m = re.search(r"<AssemblyName>([^<]+)</AssemblyName>", text)
            return m.group(1) if m else projects[0].stem
        if directory == SRC:
            break
    raise SystemExit(f"{manifest}: no project")


def features():
    result = {}
    for manifest in sorted(SRC.rglob("Manifest.cs")):
        if skip(manifest):
            continue
        where = manifest.relative_to(ROOT)
        text = strip_comments(manifest.read_text(encoding="utf-8"))
        module, declared = None, []
        for kind, body in attribute_bodies(text):
            props = {}
            for part in split_top_level(body):
                if "=" in part:
                    key, value = part.split("=", 1)
                    props[key.strip()] = value.strip()
            entry = {
                "id": resolve(props["Id"], where) if "Id" in props else None,
                "dependencies": resolve_list(props["Dependencies"], where) if "Dependencies" in props else [],
                "enabledByDependencyOnly": props.get("EnabledByDependencyOnly") == "true",
                "defaultTenantOnly": props.get("DefaultTenantOnly") == "true",
                "isAlwaysEnabled": props.get("IsAlwaysEnabled") == "true",
                "kind": kind,
                "manifest": str(where),
            }
            if kind in ("Module", "Theme"):
                module = entry
            else:
                declared.append(entry)
        if module is None:
            continue
        if module["id"] is None:
            module["id"] = assembly_name(manifest)
        for entry in declared or [module]:
            if entry["id"] is None:
                entry["id"] = module["id"]
            result[entry["id"]] = entry
    return result


def recipe_features(path):
    text = pathlib.Path(path).read_text(encoding="utf-8")
    recipe = json.loads(text)
    enabled = []
    for step in recipe.get("steps", []):
        if step.get("name") == "feature":
            enabled += step.get("enable", [])
    return recipe, enabled


def check(path):
    declared = features()
    recipe, enabled = recipe_features(path)
    excluded = recipe.get("excludedFeatures", {})
    failures = []
    for feature_id in sorted(declared):
        if feature_id in enabled and feature_id in excluded:
            failures.append(f"both enabled and excluded: {feature_id}")
        elif feature_id not in enabled and feature_id not in excluded:
            failures.append(f"neither enabled nor excluded (with a reason): {feature_id}")
    for feature_id in enabled:
        if feature_id not in declared and feature_id != "Application.Default":
            failures.append(f"enabled but declared by no manifest: {feature_id}")
    for feature_id, reason in excluded.items():
        if feature_id not in declared:
            failures.append(f"excluded but declared by no manifest: {feature_id}")
        elif not str(reason).strip():
            failures.append(f"excluded without a reason: {feature_id}")
    if len(enabled) != len(set(enabled)):
        failures.append("the feature step lists an id twice")
    for line in failures:
        print(line)
    print(f"{len(declared)} declared, {len(enabled)} enabled, {len(excluded)} excluded, {len(failures)} difference(s)")
    return 1 if failures else 0


def main(argv):
    if len(argv) > 1 and argv[1] == "--check":
        return check(argv[2])
    declared = features()
    if len(argv) > 1 and argv[1] == "--json":
        print(json.dumps(declared, indent=2))
    else:
        print("\n".join(sorted(declared)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
