import observeAndInit from "@grove/bloom/helpers/observeAndInit";
import initEasyMdeEditor from "@grove/bloom/components/easymde-editor";
import { getDatasetBoolean, getDatasetJson } from "@grove/bloom/helpers/dataset";

observeAndInit(".markdown-field-wysiwyg-editor", (wrapper) => {
    const markdownElement = wrapper.querySelector<HTMLTextAreaElement>("textarea");

    initEasyMdeEditor(markdownElement, getDatasetBoolean(wrapper, "isRtl"), getDatasetJson(wrapper, "options"));
});
