# Crest.Workflows.Designer

Shared components for the Elsa Studio visual workflow designer.

## Features

This module provides the core shared components and configuration for the Elsa Studio workflow designer when integrated with Crest.

### Core Components

- **Elsa Studio Integration** - Integrates Elsa Studio UI components with Crest
- **Workflow Designer Core** - Core workflow design and editing functionality
- **Shell Configuration** - Shell and module configuration for the designer

This module serves as the foundation for both the Blazor Server and Blazor WebAssembly implementations of the workflow designer.

## Implementation Modules

Choose one of the following implementation modules based on your deployment preferences:

- **Crest.Workflows.Designer.BlazorServer** - Server-side rendering (recommended for most scenarios)
- **Crest.Workflows.Designer.BlazorWasm** - Client-side rendering (for offline or low-latency requirements)

## Dependencies

This module requires no Crest-specific dependencies but provides shared functionality for the designer implementations.

## Package Information

- **Package ID**: `Crest.Workflows.Designer`
- **Category**: Crest Workflows
