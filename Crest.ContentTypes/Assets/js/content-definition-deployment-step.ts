import observeAndInit from "@crest/bloom/helpers/observeAndInit";
import {
    initReverseToggle,
    initCheckboxLink,
    initCheckboxCheckedLink,
    initCheckboxUncheckedLink,
} from "@crest/bloom/components/checkbox-relations";

observeAndInit("[data-reversetoggle]", initReverseToggle);
observeAndInit("[data-checkbox]", initCheckboxLink);
observeAndInit("[data-checkboxchecked]", initCheckboxCheckedLink);
observeAndInit("[data-checkboxunchecked]", initCheckboxUncheckedLink);
