import observeAndInit from "@grove/bloom/helpers/observeAndInit";
import initTrumbowygEditor from "@grove/bloom/components/trumbowyg-editor";
import { getDatasetBoolean, getDatasetJson } from "@grove/bloom/helpers/dataset";

observeAndInit(".html-body-part-trumbowyg-editor", (wrapper) => {
    const element = wrapper.querySelector<HTMLTextAreaElement>("textarea");

    if (!element) {
        return;
    }

    initTrumbowygEditor({
        element,
        languageCode: wrapper.dataset.languageCode ?? "",
        isRtl: getDatasetBoolean(wrapper, "isRtl"),
        languageDirection: wrapper.dataset.languageDirection ?? "",
        customOptions: getDatasetJson(wrapper, "options"),
    });
});
