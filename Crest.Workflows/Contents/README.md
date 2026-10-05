# Crest.Workflows.Contents

Provides Elsa workflow activities for Orchard Core content management.

## Features

This module integrates Elsa Workflows with Orchard Core's content management system, enabling workflows to interact with content items.

### Content Activities

**Content Operations:**
- **CreateContent** - Create new content items
- **UpdateContent** - Update existing content items
- **GetContent** - Retrieve content items by ID or alias
- **PublishContent** - Publish draft content items
- **UnpublishContent** - Unpublish published content items
- **DeleteContent** - Delete content items

### Content Event Triggers

These activities trigger workflows in response to content lifecycle events:

- **ContentCreated** - Triggers when a content item is created
- **ContentUpdated** - Triggers when a content item is updated
- **ContentPublished** - Triggers when a content item is published
- **ContentUnpublished** - Triggers when a content item is unpublished
- **ContentDeleted** - Triggers when a content item is deleted
- **ContentDraftSaved** - Triggers when a content draft is saved
- **ContentVersioned** - Triggers when a content item is versioned

## Dependencies

- `Crest.Workflows`
- `OrchardCore.Contents`
- `OrchardCore.Title`

## Installation

Enable the **Content Activities** feature in the Orchard Core admin dashboard under Features.

## Package Information

- **Project**: `Crest.Workflows.Contents` (feature `Crest.Workflows.Contents`)
- **Category**: Crest.Workflows
