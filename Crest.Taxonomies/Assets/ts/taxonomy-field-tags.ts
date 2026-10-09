import observeAndInit from "@crest/bloom/helpers/observeAndInit";
import { getDatasetJson } from "@crest/bloom/helpers/dataset";
import initTagsEditor, { TagTerm } from "@crest/bloom/components/tags-editor";

observeAndInit(".tags", (element) => {
    const allTagTerms = getDatasetJson<TagTerm[]>(element, "allTagTerms") ?? [];
    const translations = getDatasetJson<Record<string, string>>(element, "translations");

    if (!translations) {
        return;
    }

    initTagsEditor({
        element,
        allTagTerms,
        open: element.dataset.open === "true",
        leavesOnly: element.dataset.leavesOnly === "true",
        unique: element.dataset.unique === "true",
        taxonomyContentItemId: element.dataset.taxonomyContentItemId ?? "",
        createTagUrl: element.dataset.createTagUrl ?? "",
        createTagErrorMessage: element.dataset.createTagErrorMessage ?? "",
        hiddenInputId: element.dataset.selectedIdsInputId ?? "",
        hiddenInputName: element.dataset.selectedIdsInputName ?? "",
        translations,
        placeholder: element.dataset.placeholder,
    });
});
