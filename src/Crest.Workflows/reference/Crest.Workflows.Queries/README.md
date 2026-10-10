# Crest.Workflows.Queries

Provides Elsa workflow activities for executing Crest queries.

## Features

This module integrates Elsa Workflows with Crest's query system, enabling workflows to execute queries and process the results.

### Query Activities

The module provides activities for:

- **Execute SQL Queries** - Run SQL queries defined in Crest's query system
- **Process Query Results** - Access and manipulate query result data in workflows
- **Dynamic Data Retrieval** - Fetch data dynamically based on workflow context
- **Data-Driven Workflows** - Use query results to control workflow logic

### Integration Points

- Seamless integration with Crest's query management
- Support for parameterized queries
- Access to all query sources configured in Crest
- Result set processing and transformation

## Use Cases

- Content reporting and analytics
- Data export workflows
- Conditional logic based on database queries
- Automated data processing and transformation
- Integration with external systems using query results

## Dependencies

- `Crest.Workflows`
- `Crest.Queries.Sql`

## Installation

Enable the **Query Activities** feature in the Crest admin dashboard under Features.

## Package Information

- **Package ID**: `Crest.Workflows.Queries`
- **Category**: Crest Workflows
