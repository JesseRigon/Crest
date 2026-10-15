import initBulkSelectList from "@grove/bloom/components/bulk-select-list";
import initListSearchFilter from "@grove/bloom/components/list-search-filter";

const root = document.querySelector<HTMLElement>(".bulk-select-list");

if (root) {
    initBulkSelectList(root);
}

const searchBox = document.querySelector<HTMLInputElement>("#search-box");

if (searchBox) {
    initListSearchFilter(searchBox);
}
