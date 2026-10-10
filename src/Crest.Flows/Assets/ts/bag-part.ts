import observeAndInit from "@crest/bloom/helpers/observeAndInit";
import initFlowSortablePlaceholder from "./flow-sortable-placeholder";

observeAndInit(".bagpart-sortable-placeholder", (placeholder) =>
    initFlowSortablePlaceholder(placeholder, "bagpart-widgets"),
);
