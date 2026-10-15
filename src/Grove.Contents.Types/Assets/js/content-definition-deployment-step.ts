import observeAndInit from "@grove/bloom/helpers/observeAndInit";
import {
    initReverseToggle,
    initCheckboxLink,
    initCheckboxCheckedLink,
    initCheckboxUncheckedLink,
} from "@grove/bloom/components/checkbox-relations";

observeAndInit("[data-reversetoggle]", initReverseToggle);
observeAndInit("[data-checkbox]", initCheckboxLink);
observeAndInit("[data-checkboxchecked]", initCheckboxCheckedLink);
observeAndInit("[data-checkboxunchecked]", initCheckboxUncheckedLink);
