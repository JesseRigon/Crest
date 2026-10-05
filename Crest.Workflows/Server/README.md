# Crest.Workflows

Core Elsa Workflows integration for Orchard Core CMS.

## Features

This module provides the foundational integration between Elsa Workflows and Orchard Core, including:

### Workflow Services
- Complete Elsa Workflows runtime integration
- Workflow management and execution infrastructure
- Distributed workflow runtime support with resilience policies
- JavaScript and Liquid expression language support

### HTTP Activities
- HTTP request and response handling activities
- Webhook endpoints for triggering workflows
- HTTP middleware integration for workflow execution

### Administration UI
- Workflow management pages in the Crest admin (Definitions, Instances, Connections, Approvals)
- Workflow definition browser and editor access
- Security and permissions management for workflow operations

### Integration Features
- Role-based access control for workflows
- Orchard Core content management integration
- Admin menu and navigation integration

## Dependencies

- `OrchardCore.Contents`
- `OrchardCore.Workflows`
- `OrchardCore.Crest`

## Installation

Enable the **Crest Workflows** feature in the Orchard Core admin dashboard under Features.

## Package Information

- **Project**: `Crest.Workflows` (features `Crest.Workflows`, `Crest.Workflows.Http`)
- **Category**: Crest.Workflows
