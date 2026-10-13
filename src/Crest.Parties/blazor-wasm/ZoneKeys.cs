using Crest.DisplayManagement.Components;

// The open zone keys this library's pages own (decision 6, docs/page-regions.md): one pane and
// one detail region per party type, filled by downstream modules' page-region contributors.
[assembly: ZoneKey("Parties.Pane:*", DisplayName = "Party type pane")]
[assembly: ZoneKey("Parties.Detail:*", DisplayName = "Party record detail")]
