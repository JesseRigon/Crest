import observeAndInit from "@crest/bloom/helpers/observeAndInit";
import initEasyMdeEditor from "@crest/bloom/components/easymde-editor";
import { getDatasetBoolean, getDatasetJson } from "@crest/bloom/helpers/dataset";

observeAndInit(".markdown-field-wysiwyg-editor", (wrapper) => {
    const markdownElement = wrapper.querySelector<HTMLTextAreaElement>("textarea");

    initEasyMdeEditor(markdownElement, getDatasetBoolean(wrapper, "isRtl"), getDatasetJson(wrapper, "options"));
});
