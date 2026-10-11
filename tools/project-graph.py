#!/usr/bin/env python3
"""Project dependency graph for src/: parses every csproj (the reference/ diff-only trees excluded),
resolves ProjectReference paths (backslashes and $(PlatformSrcDir)), and writes an
interactive HTML graph (vis-network) plus a JSON edge list.

Usage: tools/project-graph.py [--out docs/dependencies.html] [--include-tests]
"""
import json, os, re, sys, html
from pathlib import Path
from xml.etree import ElementTree

root = Path(__file__).resolve().parents[1]
src = root / "src"
out = Path(sys.argv[sys.argv.index("--out") + 1]) if "--out" in sys.argv else root / "docs" / "dependencies.html"
include_tests = "--include-tests" in sys.argv
EXCLUDE = ("/reference/", "/bin/", "/obj/", "/node_modules/")
VENDORED = ("/engine/", "/designer/")

def norm(p: str) -> Path:
    p = p.replace("$(PlatformSrcDir)", str(src) + "/").replace("$(MSBuildThisFileDirectory)", "")
    return Path(p.replace("\\", "/"))

projects = {}
for csproj in src.rglob("*.csproj"):
    s = str(csproj)
    if any(x in s for x in EXCLUDE):
        continue
    if not include_tests and ("/tests/" in s or "/test/" in s or csproj.stem.endswith(".Tests")):
        continue
    projects[csproj.resolve()] = csproj.stem
vendored = {name for path, name in projects.items() if any(x in str(path) for x in VENDORED)}

edges, packages = [], {}
for path, name in projects.items():
    try:
        tree = ElementTree.parse(path)
    except ElementTree.ParseError as e:
        print("skip", path, e, file=sys.stderr); continue
    for el in tree.iter():
        tag = el.tag.split("}")[-1]
        inc = el.get("Include")
        if not inc:
            continue
        if tag == "ProjectReference":
            target = norm(inc)
            if not target.is_absolute():
                target = (path.parent / target)
            target = Path(os.path.normpath(target))
            if target in projects:
                edges.append((name, projects[target]))
            else:
                edges.append((name, target.stem + " (?)"))
        elif tag == "PackageReference":
            packages.setdefault(name, set()).add(inc)

names = sorted(set(projects.values()) | {t for _, t in edges})
kind = {}
for n in names:
    if n.endswith("(?)"): kind[n] = "missing"
    elif n in vendored: kind[n] = "engine"
    elif n.endswith(".Abstractions"): kind[n] = "abstractions"
    elif n.endswith(".Core"): kind[n] = "core"
    elif ".Targets" in n or n.endswith(".Build"): kind[n] = "targets"
    elif n.startswith("Crest.") and (src / n).exists() and (src / n / "Manifest.cs").exists(): kind[n] = "module"
    else: kind[n] = "library"
colors = {"abstractions": "#8ecae6", "core": "#219ebc", "module": "#ffb703", "library": "#adb5bd", "engine": "#b5a2d8", "targets": "#e9c46a", "missing": "#e76f51"}
indeg = {n: 0 for n in names}; outdeg = {n: 0 for n in names}
for a, b in edges: outdeg[a] += 1; indeg[b] += 1
nodes = [{"id": n, "label": n, "color": colors[kind[n]], "kind": kind[n], "value": 1 + indeg[n], "title": f"{n}\\nreferenced by {indeg[n]}, references {outdeg[n]}, packages {len(packages.get(n, ()))}"} for n in names]
data = {"nodes": nodes, "edges": [{"from": a, "to": b, "arrows": "to"} for a, b in edges], "packages": {k: sorted(v) for k, v in packages.items()}}
out.parent.mkdir(parents=True, exist_ok=True)
out.with_suffix(".json").write_text(json.dumps(data, indent=1))
out.write_text(f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Crest project dependencies</title>
<script src="https://cdnjs.cloudflare.com/ajax/libs/vis-network/9.1.9/dist/vis-network.min.js"></script>
<style>
 :root{{--bg:#fff;--fg:#222;--muted:#6b7280;--line:#d9dde3;--panel:#f6f7f9;--accent:#2a6f97}}
 html,body{{margin:0;height:100%;font:13px/1.4 system-ui,sans-serif;background:var(--bg);color:var(--fg)}}
 body{{display:flex;flex-direction:column}}
 #bar{{display:flex;flex-wrap:wrap;align-items:center;gap:6px 14px;padding:6px 12px;border-bottom:1px solid var(--line);background:var(--panel)}}
 .brand{{display:flex;align-items:baseline;gap:10px;margin-right:4px}}
 .brand h1{{font-size:14px;font-weight:600;margin:0;white-space:nowrap}}
 .count{{color:var(--muted);font-size:12px;white-space:nowrap;font-variant-numeric:tabular-nums}}
 .group{{display:flex;align-items:center;gap:6px;min-width:0}}
 .group.grow{{flex:1 1 220px}}
 .group>label{{color:var(--muted);font-size:12px;white-space:nowrap}}
 label.check{{display:flex;align-items:center;gap:4px;color:var(--fg);cursor:pointer}}
 #q{{width:100%;min-width:0;height:28px;padding:0 10px;border:1px solid var(--line);border-radius:6px;background:var(--bg);color:var(--fg);font:inherit}}
 input[type=number],select{{height:28px;padding:0 6px;border:1px solid var(--line);border-radius:6px;background:var(--bg);color:var(--fg);font:inherit}}
 input[type=number]{{width:4.5em}}
 input[type=checkbox]{{margin:0;accent-color:var(--accent)}}
 :is(#q,input[type=number],select):focus{{outline:2px solid var(--accent);outline-offset:1px}}
 .legend{{display:flex;gap:10px;flex-wrap:wrap;font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:.04em;margin-left:auto}}
 .sw{{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:4px;vertical-align:-1px}}
 button{{height:28px;padding:0 10px;border:1px solid var(--line);border-radius:6px;background:var(--bg);color:var(--fg);font:inherit;cursor:pointer}}
 #main{{flex:1 1 auto;min-height:0;position:relative}}
 #net{{position:absolute;inset:0}}
 #info{{display:none;position:absolute;left:0;right:0;bottom:0;max-height:38%;overflow:hidden;border-top:1px solid var(--line);background:color-mix(in srgb,var(--panel) 92%,transparent);backdrop-filter:blur(4px);flex-direction:column;box-shadow:0 -6px 20px rgba(0,0,0,.08)}}
 #info.open{{display:flex}}
 #info header{{display:flex;align-items:center;gap:10px;padding:6px 12px;border-bottom:1px solid var(--line)}}
 #info header b{{font-family:ui-monospace,monospace;font-size:13px}}
 #info header .count{{margin-right:auto}}
 #info .cols{{display:grid;grid-template-columns:1fr 1fr 1fr;gap:0;min-height:0;overflow:hidden;flex:1 1 auto}}
 #info .col{{min-width:0;overflow:auto;padding:6px 12px;border-right:1px solid var(--line)}}
 #info .col:last-child{{border-right:0}}
 #info h3{{font-size:11px;margin:0 0 4px;color:var(--muted);text-transform:uppercase;letter-spacing:.05em}}
 #info ul{{margin:0;padding:0;list-style:none;font:11.5px/1.5 ui-monospace,monospace}}
 #info li[data-id]{{cursor:pointer}}#info li[data-id]:hover{{color:var(--accent)}}
 @media (max-width:700px){{#info .cols{{grid-template-columns:1fr 1fr}}#info .col.pk{{display:none}}}}
 @media (max-width:700px){{.legend{{margin-left:0}}.brand{{width:100%}}}}
</style></head><body>
<div id="bar">
 <div class="brand"><h1>Crest project dependencies</h1><span class="count">{len(projects)} projects · {len(edges)} references</span></div>
 <div class="group grow"><input id="q" type="search" placeholder="Filter projects (regex), e.g. Access|Infrastructure" aria-label="Filter projects"></div>
 <div class="group"><label for="depth">Depth</label><input type="number" id="depth" min="1" max="6" value="1"></div>
 <div class="group"><label for="dir">Arrows</label><select id="dir"><option value="both">both ways</option><option value="out">references only</option><option value="in">referenced by only</option></select></div>
 <div class="group"><label class="check"><input type="checkbox" id="hidetargets" checked> Hide targets</label></div>
 <div class="group"><label for="hub">Hide hubs ≥</label><input type="number" id="hub" min="1" placeholder="off"></div>
 <span id="status" class="count"></span><button id="relayout" type="button">Re-layout</button>
 <div class="legend">{"".join(f'<span><span class="sw" style="background:{c}"></span>{k}</span>' for k, c in colors.items())}</div>
</div>
<div id="main"><div id="net"></div><div id="info"><header><b id="infotitle"></b><span class="count" id="infocount"></span><span class="count"><span class="sw" style="background:#2a6f97"></span>references <span class="sw" style="background:#d98e04"></span>referenced by</span><button id="close" type="button" aria-label="Close">×</button></header>
<div class="cols"><div class="col"><h3>References</h3><ul id="refs"></ul></div><div class="col"><h3>Referenced by</h3><ul id="by"></ul></div><div class="col pk"><h3>Packages</h3><ul id="pk"></ul></div></div></div></div>
<script>
const data = {json.dumps(data)};

// One layout per data set. The force simulation runs once over the full graph; its result
// is the layout, kept in this browser under a key derived from the data, and every later
// view (filter, focus, back to the full graph) shows or hides nodes at those positions.
// Re-layout recomputes on demand.
const layoutKey = 'crest-deps-layout-v2:' + data.nodes.length + ':' + data.edges.length;
let layout = null;
try {{ layout = JSON.parse(localStorage.getItem(layoutKey) || 'null'); }} catch {{ layout = null; }}
const shown = {{nodes: new vis.DataSet([]), edges: new vis.DataSet([])}};
const net = new vis.Network(document.getElementById('net'), shown, {{
  layout: {{improvedLayout: false}},
  physics: false,
  nodes: {{shape: 'dot', scaling: {{min: 5, max: 28}}, font: {{size: 11}}, chosen: {{node: true, label: (values, id, selected, hovering) => {{ values.strokeWidth = 6; values.strokeColor = '#fff'; values.size = 13; }}}}}},
  edges: {{color: {{color: '#888', highlight: '#e76f51', opacity: 0.25}}, smooth: false, arrows: 'to'}},
  interaction: {{hover: true, dragNodes: false}}
}});
let focusId = null;
function placed(n) {{ const p = layout && layout[n.id]; return p ? {{...n, x: p.x, y: p.y}} : n; }}
function computeLayout() {{
  layout = null;
  shown.nodes.clear(); shown.edges.clear();
  shown.nodes.add(data.nodes); shown.edges.add(data.edges);
  document.getElementById('status').textContent = 'computing layout…';
  net.setOptions({{physics: {{solver: 'forceAtlas2Based', forceAtlas2Based: {{gravitationalConstant: -40, springLength: 80}}, stabilization: {{iterations: 400, updateInterval: 50}}}}}});
  net.once('stabilizationIterationsDone', () => {{
    net.setOptions({{physics: false}});
    layout = net.getPositions();
    try {{ localStorage.setItem(layoutKey, JSON.stringify(layout)); }} catch {{ /* no storage: recomputed next open */ }}
    document.getElementById('status').textContent = '';
    apply();
  }});
  net.stabilize(400);
}}
function apply() {{
  if (!layout) return;
  const q = document.getElementById('q').value.trim();
  const depth = Math.max(1, parseInt(document.getElementById('depth').value, 10) || 1);
  const dir = document.getElementById('dir').value;
  const hideTargets = document.getElementById('hidetargets').checked;
  const hubMax = parseInt(document.getElementById('hub').value, 10);
  const indeg = {{}}; for (const e of data.edges) indeg[e.to] = (indeg[e.to] || 0) + 1;
  const hidden = new Set(data.nodes.filter(n => (hideTargets && n.kind === 'targets') || (hubMax > 0 && (indeg[n.id] || 0) >= hubMax)).map(n => n.id));
  let edges = data.edges.filter(e => !hidden.has(e.from) && !hidden.has(e.to));
  let keep = new Set(data.nodes.map(n => n.id));
  if (focusId || q) {{
    let hit;
    if (focusId) hit = new Set([focusId]);
    else {{ let re; try {{ re = new RegExp(q, 'i'); }} catch {{ return; }} hit = new Set(data.nodes.filter(n => re.test(n.id)).map(n => n.id)); }}
    keep = new Set(hit);
    if (focusId) {{
      // Depth counts connections: only the edges walked outward from the matched nodes are
      // drawn, never the edges among the neighbours themselves.
      let frontier = new Set(hit); const walked = [];
      for (let level = 0; level < depth; level++) {{
        const next = new Set();
        // Arrows leaving the matched nodes (references) and arrows entering them
        // (referenced by) are drawn in two colours.
        const grey = e => ({{...e, color: {{color: '#888', highlight: '#888', opacity: 0.4}}}});
        const out = e => level === 0 ? {{...e, color: {{color: '#2a6f97', highlight: '#2a6f97', opacity: 1}}}} : grey(e);
        const inn = e => level === 0 ? {{...e, color: {{color: '#d98e04', highlight: '#d98e04', opacity: 1}}}} : grey(e);
        for (const e of edges) {{
          if (dir !== 'in' && frontier.has(e.from) && !keep.has(e.to)) {{ next.add(e.to); walked.push(out(e)); }}
          else if (dir !== 'out' && frontier.has(e.to) && !keep.has(e.from)) {{ next.add(e.from); walked.push(inn(e)); }}
          else if (level === 0 && hit.has(e.from) && hit.has(e.to)) walked.push(out(e));
        }}
        for (const id of next) keep.add(id);
        frontier = next;
      }}
      edges = walked;
    }}
  }}
  for (const id of hidden) keep.delete(id);
  shown.nodes.clear(); shown.edges.clear();
  shown.nodes.add(data.nodes.filter(n => keep.has(n.id)).map(placed));
  shown.edges.add(edges.filter(e => keep.has(e.from) && keep.has(e.to)));
}}
for (const id of ['q', 'hub', 'depth']) document.getElementById(id).addEventListener('input', apply);
for (const id of ['dir', 'hidetargets']) document.getElementById(id).addEventListener('change', apply);
document.getElementById('relayout').addEventListener('click', () => {{ try {{ localStorage.removeItem(layoutKey); }} catch {{}} computeLayout(); }});
function hideInfo() {{ document.getElementById('info').classList.remove('open'); }}
function show(id) {{
  const refs = data.edges.filter(e => e.from === id).map(e => e.to).sort();
  const by = data.edges.filter(e => e.to === id).map(e => e.from).sort();
  const pk = data.packages[id] || [];
  const li = xs => xs.map(x => '<li data-id="' + x + '">' + x + '</li>').join('');
  document.getElementById('infotitle').textContent = id;
  document.getElementById('infocount').textContent = refs.length + ' references · ' + by.length + ' referenced by · ' + pk.length + ' packages';
  document.getElementById('refs').innerHTML = li(refs);
  document.getElementById('by').innerHTML = li(by);
  document.getElementById('pk').innerHTML = pk.map(p => '<li>' + p + '</li>').join('');
  document.getElementById('info').classList.add('open');
}}
document.getElementById('info').addEventListener('click', e => {{ const id = e.target.dataset && e.target.dataset.id; if (id && data.nodes.some(n => n.id === id)) {{ focusId = id; apply(); show(id); net.selectNodes([id]); }} }});
document.getElementById('close').addEventListener('click', () => {{ hideInfo(); if (focusId) {{ focusId = null; apply(); }} }});
net.on('click', p => {{
  if (p.nodes.length) {{ focusId = p.nodes[0]; apply(); net.selectNodes([focusId]); show(focusId); }}
  else if (focusId) {{ focusId = null; apply(); hideInfo(); }}
}});
if (layout) apply(); else computeLayout();
</script></body></html>''')
missing = sorted({t for _, t in edges if t.endswith("(?)")})
print(f"{len(projects)} projects, {len(edges)} references, {len(missing)} unresolved -> {out}")
for m in missing: print("  unresolved:", m)
