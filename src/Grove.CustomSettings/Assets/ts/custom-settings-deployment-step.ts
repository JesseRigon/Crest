import observeAndInit from "@grove/bloom/helpers/observeAndInit";
import { initReverseToggle } from "@grove/bloom/components/checkbox-relations";

observeAndInit("[data-reversetoggle]", initReverseToggle);
