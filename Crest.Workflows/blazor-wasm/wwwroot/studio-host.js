// Loads the classic scripts Studio's components call into (MudBlazor, the MudBlazor
// extensions, Monaco's AMD loader and editor), once, in order. Blazor does not run
// <script> tags it renders, so the workflow pages load them through this module.
const base = document.baseURI;
const scripts = [
  '_content/MudBlazor/MudBlazor.min.js',
  '_content/CodeBeam.MudBlazor.Extensions/MudExtensions.min.js',
  '_content/BlazorMonaco/jsInterop.js',
  '_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js',
  '_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js',
];

let loading;

function load(src) {
  const url = new URL(src, base).href;
  if (document.querySelector(`script[data-studio-src="${url}"]`)) return Promise.resolve();
  return new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = url;
    script.dataset.studioSrc = url;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Could not load ${src}`));
    document.head.appendChild(script);
  });
}

export function loadStudioAssets() {
  loading ??= scripts.reduce((chain, src) => chain.then(() => load(src)), Promise.resolve());
  return loading;
}
