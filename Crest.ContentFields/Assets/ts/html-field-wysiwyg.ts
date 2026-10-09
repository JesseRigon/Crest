import observeAndInit from "@crest/bloom/helpers/observeAndInit";
import initTrumbowygEditor from "@crest/bloom/components/trumbowyg-editor";
import { getDatasetBoolean } from "@crest/bloom/helpers/dataset";

observeAndInit(".html-field-wysiwyg-editor", (wrapper) => {
    const element = wrapper.querySelector<HTMLTextAreaElement>("textarea");

    if (!element) {
        return;
    }

    initTrumbowygEditor({
        element,
        languageCode: wrapper.dataset.languageCode ?? "",
        isRtl: getDatasetBoolean(wrapper, "isRtl"),
        languageDirection: wrapper.dataset.languageDirection ?? "",
        extendDefaultButtons: true,
    });
});
