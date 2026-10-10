import observeAndInit from "@crest/bloom/helpers/observeAndInit";
import { initReverseToggle } from "@crest/bloom/components/checkbox-relations";

observeAndInit("[data-reversetoggle]", initReverseToggle);
