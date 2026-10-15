// The code editor (CodeEditor): Monaco, loaded once from Grove.Display.Resources' monaco-esm loader (the
// one Monaco instance every editor on the page shares), one editor per element, its text handed
// back to the component as it changes.
let loading;

function tenantBase() {
    const meta = document.querySelector('meta[name="grove-tenant-base"]');
    return (meta && meta.content ? meta.content : '').replace(/\/$/, '');
}

function addScript(src) {
    return new Promise((resolve, reject) => {
        const element = document.createElement('script');
        element.src = src;
        element.onload = () => resolve();
        element.onerror = () => { element.remove(); reject(new Error('Could not load ' + src)); };
        document.head.appendChild(element);
    });
}

function load() {
    if (window.__platformMonacoReady) {
        return window.__platformMonacoReady;
    }

    if (!loading) {
        const root = tenantBase() + '/Grove.Display.Resources/Scripts/monaco-esm/';
        if (!document.querySelector('link[data-grove-monaco]')) {
            const style = document.createElement('link');
            style.rel = 'stylesheet';
            style.href = root + 'monaco-loader.css';
            style.setAttribute('data-grove-monaco', '');
            document.head.appendChild(style);
        }

        loading = addScript(root + 'monaco-loader.min.js')
            .catch(() => addScript(root + 'monaco-loader.js'))
            .then(() => window.__platformMonacoReady);
    }

    return loading;
}

function theme() {
    const dark = document.documentElement.getAttribute('data-theme') === 'dark'
        || document.body.classList.contains('dark')
        || (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches && document.documentElement.getAttribute('data-theme') !== 'light');
    return dark ? 'vs-dark' : 'vs';
}

export async function create(element, value, language, readOnly, component) {
    const monaco = await load();
    const editor = monaco.editor.create(element, {
        value: value || '',
        language: language || 'plaintext',
        readOnly: !!readOnly,
        automaticLayout: true,
        minimap: { enabled: false },
        lineNumbers: 'on',
        scrollBeyondLastLine: false,
        theme: theme(),
    });

    let timer;
    editor.onDidChangeModelContent(() => {
        clearTimeout(timer);
        timer = setTimeout(() => component.invokeMethodAsync('SetValue', editor.getValue()), 250);
    });
    editor.onDidBlurEditorText(() => {
        clearTimeout(timer);
        component.invokeMethodAsync('SetValue', editor.getValue());
    });

    return { editor };
}

export function update(handle, value, language, readOnly) {
    if (!handle || !handle.editor) {
        return;
    }

    const editor = handle.editor;
    if ((value || '') !== editor.getValue()) {
        editor.setValue(value || '');
    }

    const model = editor.getModel();
    if (model && window.__platformMonacoReady) {
        window.__platformMonacoReady.then(monaco => monaco.editor.setModelLanguage(model, language || 'plaintext'));
    }

    editor.updateOptions({ readOnly: !!readOnly });
}

export function dispose(handle) {
    if (handle && handle.editor) {
        handle.editor.dispose();
    }
}
