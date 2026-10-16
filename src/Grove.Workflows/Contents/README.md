# Grove.Workflows.Contents

Provides Elsa workflow activities for Grove content management.

## Features

This module integrates Elsa Workflows with Grove's content management system, enabling workflows to interact with content items.

### Content Activities

**Content Operations:**
- **CreateContent** - Create new content items
- **UpdateContent** - Update existing content items
- **GetContent** - Retrieve content items by ID or alias
- **PublishContent** - Publish draft content items
- **UnpublishContent** - Unpublish published content items
- **DeleteContent** - Delete content items

### Content lifecycle triggers

One trigger per content lifecycle point, raised once the unit that made the change commits,
filtered by content type; the result is the item's new state and `PreviousContentItem` its
state before the change (docs/content-engine.md › Behaviour is workflows):

- **ContentCreated**, **ContentUpdated**, **ContentValidated**, **ContentDraftSaved**
- **ContentPublished**, **ContentUnpublished**, **ContentRemoved**
- **ContentCloned**, **ContentImported**, **ContentLocalized**

### Content lifecycle hooks

Each content type has a hook slot per lifecycle point (`content:{Type}.{point}`), run inside the
saving unit before the item is stored; see docs/workflows.md › Hook slots.

## Dependencies

- `Grove.Workflows`
- `Grove.Contents`
- `Grove.Contents.Title`

## Installation

Enable the **Content Activities** feature in the Grove admin dashboard under Features.

## Package Information

- **Project**: `Grove.Workflows.Contents` (feature `Grove.Workflows.Contents`)
- **Category**: Grove.Workflows
