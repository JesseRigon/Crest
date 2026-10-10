import observeAndInit from "@crest/bloom/helpers/observeAndInit";

const initGoogleSchemaEditor = (element: HTMLElement) => {
    const textArea = element as HTMLTextAreaElement;

    CodeMirror.fromTextArea(textArea, {
        autoRefresh: true,
        lineNumbers: true,
        lineWrapping: true,
        matchBrackets: true,
        styleActiveLine: true,
        mode: { name: "javascript" },
    });
};

observeAndInit(".seo-google-schema-editor", initGoogleSchemaEditor);
